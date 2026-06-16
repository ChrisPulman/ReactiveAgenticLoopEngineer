using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RALE.Server.Data;
using RALE.Server.Models;
using RALE.Server.Services;

namespace RALE.Tests;

public sealed class OrchestrationEngineerTests
{
    [Test]
    public async Task RegisterAgent_persists_agent_card_profile()
    {
        await using var harness = await RaleHarness.CreateAsync();

        var agent = await harness.Orchestrator.RegisterAgentAsync(new AgentCard(
            "csharp-agent",
            ["csharp", "mcp"],
            "https://agent.example",
            2,
            128,
            ["implementation"],
            "p95<2m",
            "trusted",
            90,
            ["repo.read", "tests.run"],
            60));

        var agents = await harness.Orchestrator.ListAgentsAsync();
        await using var context = harness.ContextFactory.CreateDbContext();
        var agentEventCount = await context.AgentEvents.CountAsync();

        await Assert.That(agents).Count().IsEqualTo(1);
        await Assert.That(agent.Name).IsEqualTo("csharp-agent");
        await Assert.That(agent.MaxConcurrentGoals).IsEqualTo(2);
        await Assert.That(agent.CachedCapacity).IsEqualTo(128);
        await Assert.That(agent.SecurityPosture).IsEqualTo("trusted");
        await Assert.That(agentEventCount).IsEqualTo(1);
    }

    [Test]
    public async Task DiscoverCapacity_falls_back_to_fresh_cached_profile_when_endpoint_fails()
    {
        await using var harness = await RaleHarness.CreateAsync();
        harness.CapacityClient.NextCapacity = null;
        var agent = await harness.Orchestrator.RegisterAgentAsync(DefaultCard(maxTokenCapacity: 42));

        var capacity = await harness.Orchestrator.DiscoverCapacityAsync(agent.Id, "implementation");

        await Assert.That(capacity.Source).IsEqualTo("cache");
        await Assert.That(capacity.Capacity).IsEqualTo(42);
        await Assert.That(harness.CapacityClient.QueryCount).IsEqualTo(1);
    }

    [Test]
    public async Task CreateMasterPlan_emits_capacity_fit_tasks_with_serial_dependencies()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var small = await harness.Orchestrator.RegisterAgentAsync(DefaultCard(name: "small", maxTokenCapacity: 18));
        var large = await harness.Orchestrator.RegisterAgentAsync(DefaultCard(name: "large", maxTokenCapacity: 30));
        harness.CapacityClient.Capacities[small.Id] = 18;
        harness.CapacityClient.Capacities[large.Id] = 30;

        var loop = await harness.Orchestrator.CreateMasterPlanAsync(new MasterPlanRequest(
            "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron",
            [small.Id, large.Id],
            100,
            "implementation",
            "serial",
            ["patch", "tests"],
            "{}",
            5,
            null,
            false,
            10,
            ["repo.read"],
            3,
            2));

        var reloaded = await harness.ContextFactory.CreateDbContext().Goals
            .AsNoTracking()
            .Where(goal => goal.LoopId == loop.Id)
            .OrderBy(goal => goal.Sequence)
            .ToArrayAsync();

        await Assert.That(reloaded.Length > 1).IsTrue();
        await Assert.That(reloaded.All(goal => goal.AssignedAgentId.HasValue)).IsTrue();
        await Assert.That(reloaded.Where(goal => goal.AssignedAgentId == small.Id).All(goal => goal.Prompt.Length <= 18)).IsTrue();
        await Assert.That(reloaded.Where(goal => goal.AssignedAgentId == large.Id).All(goal => goal.Prompt.Length <= 30)).IsTrue();
        await Assert.That(ParseDependencies(reloaded[0]).Count).IsEqualTo(0);
        await Assert.That(ParseDependencies(reloaded[1]).Single()).IsEqualTo(reloaded[0].Id);
    }

    [Test]
    public async Task AssignNextGoal_waits_for_approval_when_policy_requires_review()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var agent = await harness.Orchestrator.RegisterAgentAsync(DefaultCard(trustLevel: 10));
        harness.CapacityClient.Capacities[agent.Id] = 100;
        var loop = await harness.Orchestrator.CreateMasterPlanAsync(new MasterPlanRequest(
            "single gated task",
            [agent.Id],
            100,
            "implementation",
            "parallel",
            [],
            "{}",
            0,
            null,
            false,
            50,
            ["repo.read"],
            3,
            1));

        var beforeApproval = await harness.Orchestrator.AssignNextGoalAsync(loop.Id, agent.Id);
        var gatedGoal = loop.Goals.Single();
        var approved = await harness.Orchestrator.ApproveGoalAsync(gatedGoal.Id, true, "reviewer");
        var assigned = await harness.Orchestrator.AssignNextGoalAsync(loop.Id, agent.Id);

        await Assert.That(beforeApproval).IsNull();
        await Assert.That(approved).IsNotNull();
        await Assert.That(approved!.ApprovalState).IsEqualTo("Approved");
        await Assert.That(assigned).IsNotNull();
        await Assert.That(assigned!.Status).IsEqualTo(GoalStatus.InProgress);
    }

    [Test]
    public async Task ResplitGoal_creates_smaller_replacement_tasks_and_skips_original()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var agent = await harness.Orchestrator.RegisterAgentAsync(DefaultCard(maxTokenCapacity: 100));
        harness.CapacityClient.Capacities[agent.Id] = 100;
        var loop = await harness.Orchestrator.CreateMasterPlanAsync(new MasterPlanRequest(
            "alpha beta gamma delta epsilon zeta eta theta",
            [agent.Id],
            100,
            "implementation",
            "parallel",
            [],
            "{}",
            0,
            null,
            false,
            0,
            ["repo.read"],
            3,
            1));

        var original = loop.Goals.Single();
        var replacements = await harness.Orchestrator.ResplitGoalAsync(original.Id, "capacity mismatch", 12);
        await using var context = harness.ContextFactory.CreateDbContext();
        var originalReloaded = await context.Goals.AsNoTracking().SingleAsync(goal => goal.Id == original.Id);

        await Assert.That(replacements.Count > 1).IsTrue();
        await Assert.That(replacements.All(goal => goal.Prompt.Length <= 12)).IsTrue();
        await Assert.That(originalReloaded.Status).IsEqualTo(GoalStatus.Skipped);
    }

    private static AgentCard DefaultCard(
        string name = "agent",
        int maxTokenCapacity = 100,
        int trustLevel = 90) => new(
            name,
            ["implementation"],
            "https://agent.example",
            1,
            maxTokenCapacity,
            ["implementation"],
            "standard",
            "trusted",
            trustLevel,
            ["repo.read"],
            300);

    private static Guid[] ParseDependencies(Goal goal)
    {
        if (string.IsNullOrWhiteSpace(goal.DependsOnJson))
        {
            return [];
        }

        return System.Text.Json.JsonSerializer.Deserialize<Guid[]>(goal.DependsOnJson) ?? [];
    }

    private sealed class RaleHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RaleHarness(SqliteConnection connection, TestDbContextFactory contextFactory, FakeCapacityClient capacityClient)
        {
            _connection = connection;
            ContextFactory = contextFactory;
            CapacityClient = capacityClient;
            Orchestrator = new OrchestrationEngineer(
                contextFactory,
                capacityClient,
                NullLogger<OrchestrationEngineer>.Instance);
        }

        public TestDbContextFactory ContextFactory { get; }

        public FakeCapacityClient CapacityClient { get; }

        public OrchestrationEngineer Orchestrator { get; }

        public static async Task<RaleHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<RALEContext>().UseSqlite(connection).Options;
            var factory = new TestDbContextFactory(options);

            await using var context = factory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();

            return new RaleHarness(connection, factory, new FakeCapacityClient());
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<RALEContext> options) : IDbContextFactory<RALEContext>
    {
        public RALEContext CreateDbContext() => new(options);
    }

    private sealed class FakeCapacityClient : IAgentCapacityClient
    {
        public Dictionary<Guid, int> Capacities { get; } = [];

        public AgentCapacity? NextCapacity { get; set; }

        public int QueryCount { get; private set; }

        public Task<AgentCapacity?> QueryCapacityAsync(Agent agent, string taskProfile, CancellationToken cancellationToken = default)
        {
            QueryCount++;
            if (NextCapacity is not null)
            {
                return Task.FromResult<AgentCapacity?>(NextCapacity);
            }

            if (!Capacities.TryGetValue(agent.Id, out var capacity))
            {
                return Task.FromResult<AgentCapacity?>(null);
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult<AgentCapacity?>(new AgentCapacity(
                agent.Id,
                capacity,
                agent.MaxConcurrentGoals,
                "{}",
                now,
                now.AddMinutes(5),
                "live"));
        }
    }
}
