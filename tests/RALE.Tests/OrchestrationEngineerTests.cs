// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RALE.Server.Data;
using RALE.Server.Models;
using RALE.Server.Services;

namespace RALE.Tests;

/// <summary>Verifies the persisted multi-agent orchestration workflow.</summary>
public sealed class OrchestrationEngineerTests
{
    /// <summary>Identifies implementation work.</summary>
    private const string ImplementationTaskType = "implementation";

    /// <summary>Identifies trusted agent security posture.</summary>
    private const string TrustedSecurityPosture = "trusted";

    /// <summary>Identifies read access to the repository.</summary>
    private const string RepositoryReadScope = "repo.read";

    /// <summary>Defines the default test token capacity.</summary>
    private const int DefaultTokenCapacity = 100;

    /// <summary>Defines the default test trust level.</summary>
    private const int DefaultTrustLevel = 90;

    /// <summary>Defines the default capacity cache lifetime.</summary>
    private const int DefaultCapacityCacheTtlSeconds = 300;

    /// <summary>Defines the registration capacity cache lifetime.</summary>
    private const int RegistrationCapacityCacheTtlSeconds = 60;

    /// <summary>Defines the default master-plan iteration limit.</summary>
    private const int DefaultIterationLimit = 3;

    /// <summary>Defines the default master-plan retry limit.</summary>
    private const int DefaultRetryLimit = 2;

    /// <summary>Defines the concurrently active goals for a registered agent.</summary>
    private const int AgentConcurrentGoalLimit = 2;

    /// <summary>Defines one expected item or single concurrent goal.</summary>
    private const int DefaultConcurrentGoalLimit = 1;

    /// <summary>Defines the explicitly registered capacity.</summary>
    private const int RegisteredAgentCapacity = 128;

    /// <summary>Defines the cached fallback capacity.</summary>
    private const int CachedCapacity = 42;

    /// <summary>Defines the smaller agent capacity.</summary>
    private const int SmallAgentCapacity = 18;

    /// <summary>Defines the larger agent capacity.</summary>
    private const int LargeAgentCapacity = 30;

    /// <summary>Defines the minimum trust level requiring approval.</summary>
    private const int MinimumTrustedCapacity = 50;

    /// <summary>Defines the maximum prompt size after resplitting.</summary>
    private const int ResplitPromptCapacity = 12;

    /// <summary>Defines the master-plan priority.</summary>
    private const int MasterPlanPriority = 5;

    /// <summary>Defines the reviewer trust level.</summary>
    private const int ReviewerTrustLevel = 10;

    /// <summary>Verifies that registering an agent persists its card and audit event.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegisterAgent_persists_agent_card_profile()
    {
        await using var harness = await RaleHarness.CreateAsync();

        var agent = await harness.Orchestrator.RegisterAgentAsync(new AgentCard
        {
            Name = "csharp-agent",
            Capabilities = ["csharp", "mcp"],
            Endpoint = "https://agent.example",
            MaxConcurrentGoals = AgentConcurrentGoalLimit,
            MaxTokenCapacity = RegisteredAgentCapacity,
            SupportedTaskTypes = [ImplementationTaskType],
            Sla = "p95<2m",
            SecurityPosture = TrustedSecurityPosture,
            TrustLevel = DefaultTrustLevel,
            ToolScopes = [RepositoryReadScope, "tests.run"],
            CapacityCacheTtlSeconds = RegistrationCapacityCacheTtlSeconds,
        });

        var agents = await harness.Orchestrator.ListAgentsAsync();
        await using var context = harness.ContextFactory.CreateDbContext();
        var agentEventCount = await context.AgentEvents.CountAsync();

        await Assert.That(agents).Count().IsEqualTo(DefaultConcurrentGoalLimit);
        await Assert.That(agent.Name).IsEqualTo("csharp-agent");
        await Assert.That(agent.MaxConcurrentGoals).IsEqualTo(AgentConcurrentGoalLimit);
        await Assert.That(agent.CachedCapacity).IsEqualTo(RegisteredAgentCapacity);
        await Assert.That(agent.SecurityPosture).IsEqualTo(TrustedSecurityPosture);
        await Assert.That(agentEventCount).IsEqualTo(DefaultConcurrentGoalLimit);
    }

    /// <summary>Verifies fresh cached capacity is used after an endpoint failure.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscoverCapacity_falls_back_to_fresh_cached_profile_when_endpoint_fails()
    {
        await using var harness = await RaleHarness.CreateAsync();
        harness.CapacityClient.NextCapacity = null;
        var agent = await harness.Orchestrator.RegisterAgentAsync(DefaultCard(maxTokenCapacity: CachedCapacity));

        var capacity = await harness.Orchestrator.DiscoverCapacityAsync(agent.Id, ImplementationTaskType);

        await Assert.That(capacity.Source).IsEqualTo("cache");
        await Assert.That(capacity.Capacity).IsEqualTo(CachedCapacity);
        await Assert.That(harness.CapacityClient.QueryCount).IsEqualTo(DefaultConcurrentGoalLimit);
    }

    /// <summary>Verifies a master plan produces serial tasks sized for assigned agents.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateMasterPlan_emits_capacity_fit_tasks_with_serial_dependencies()
    {
        await using var harness = await RaleHarness.CreateAsync();
        const string smallAgentName = "small";
        const string largeAgentName = "large";
        var small = await harness.Orchestrator.RegisterAgentAsync(
            DefaultCard(name: smallAgentName, maxTokenCapacity: SmallAgentCapacity));
        var large = await harness.Orchestrator.RegisterAgentAsync(
            DefaultCard(name: largeAgentName, maxTokenCapacity: LargeAgentCapacity));
        harness.CapacityClient.Capacities[small.Id] = SmallAgentCapacity;
        harness.CapacityClient.Capacities[large.Id] = LargeAgentCapacity;

        var loop = await harness.Orchestrator.CreateMasterPlanAsync(new MasterPlanRequest
        {
            PrimaryObjective = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron",
            AgentIds = [small.Id, large.Id],
            TokenLimit = DefaultTokenCapacity,
            TaskType = ImplementationTaskType,
            ExecutionPattern = "serial",
            RequiredArtifacts = ["patch", "tests"],
            ConstraintsJson = "{}",
            Priority = MasterPlanPriority,
            Deadline = null,
            ApprovalRequired = false,
            MinTrustLevel = ReviewerTrustLevel,
            ToolScopes = [RepositoryReadScope],
            IterationLimit = DefaultIterationLimit,
            RetryLimit = DefaultRetryLimit,
        });

        var reloaded = await harness.ContextFactory.CreateDbContext().Goals
            .AsNoTracking()
            .Where(goal => goal.LoopId == loop.Id)
            .OrderBy(goal => goal.Sequence)
            .ToArrayAsync();

        await Assert.That(reloaded.Length > DefaultConcurrentGoalLimit).IsTrue();
        await Assert.That(AllGoalsAreAssigned(reloaded)).IsTrue();
        await Assert.That(AllAssignedPromptsFit(reloaded, small.Id, SmallAgentCapacity)).IsTrue();
        await Assert.That(AllAssignedPromptsFit(reloaded, large.Id, LargeAgentCapacity)).IsTrue();
        await Assert.That(ParseDependencies(reloaded[0]).Length).IsEqualTo(0);
        await Assert.That(ParseDependencies(reloaded[1])[0]).IsEqualTo(reloaded[0].Id);
    }

    /// <summary>Verifies assignment waits until a reviewer approves a gated goal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignNextGoal_waits_for_approval_when_policy_requires_review()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var agent = await harness.Orchestrator.RegisterAgentAsync(DefaultCard(trustLevel: ReviewerTrustLevel));
        harness.CapacityClient.Capacities[agent.Id] = DefaultTokenCapacity;
        var loop = await harness.Orchestrator.CreateMasterPlanAsync(new MasterPlanRequest
        {
            PrimaryObjective = "single gated task",
            AgentIds = [agent.Id],
            TokenLimit = DefaultTokenCapacity,
            TaskType = ImplementationTaskType,
            ExecutionPattern = "parallel",
            RequiredArtifacts = [],
            ConstraintsJson = "{}",
            Priority = 0,
            Deadline = null,
            ApprovalRequired = false,
            MinTrustLevel = MinimumTrustedCapacity,
            ToolScopes = [RepositoryReadScope],
            IterationLimit = DefaultIterationLimit,
            RetryLimit = DefaultConcurrentGoalLimit,
        });

        var beforeApproval = await harness.Orchestrator.AssignNextGoalAsync(loop.Id, agent.Id);
        var gatedGoal = loop.Goals[0];
        var approved = await harness.Orchestrator.ApproveGoalAsync(gatedGoal.Id, true, "reviewer");
        var assigned = await harness.Orchestrator.AssignNextGoalAsync(loop.Id, agent.Id);

        await Assert.That(beforeApproval).IsNull();
        await Assert.That(approved).IsNotNull();
        await Assert.That(approved!.ApprovalState).IsEqualTo("Approved");
        await Assert.That(assigned).IsNotNull();
        await Assert.That(assigned!.Status).IsEqualTo(GoalStatus.InProgress);
    }

    /// <summary>Verifies a capacity mismatch replaces and skips the original goal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResplitGoal_creates_smaller_replacement_tasks_and_skips_original()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var agent = await harness.Orchestrator.RegisterAgentAsync(DefaultCard());
        harness.CapacityClient.Capacities[agent.Id] = DefaultTokenCapacity;
        var loop = await harness.Orchestrator.CreateMasterPlanAsync(new MasterPlanRequest
        {
            PrimaryObjective = "alpha beta gamma delta epsilon zeta eta theta",
            AgentIds = [agent.Id],
            TokenLimit = DefaultTokenCapacity,
            TaskType = ImplementationTaskType,
            ExecutionPattern = "parallel",
            RequiredArtifacts = [],
            ConstraintsJson = "{}",
            Priority = 0,
            Deadline = null,
            ApprovalRequired = false,
            MinTrustLevel = 0,
            ToolScopes = [RepositoryReadScope],
            IterationLimit = DefaultIterationLimit,
            RetryLimit = DefaultConcurrentGoalLimit,
        });

        var original = loop.Goals[0];
        var replacements = await harness.Orchestrator.ResplitGoalAsync(
            original.Id,
            "capacity mismatch",
            ResplitPromptCapacity);
        await using var context = harness.ContextFactory.CreateDbContext();
        var originalReloaded = await context.Goals.AsNoTracking().SingleAsync(goal => goal.Id == original.Id);

        await Assert.That(replacements.Count > DefaultConcurrentGoalLimit).IsTrue();
        await Assert.That(AllPromptsFit(replacements, ResplitPromptCapacity)).IsTrue();
        await Assert.That(originalReloaded.Status).IsEqualTo(GoalStatus.Skipped);
    }

    /// <summary>Creates a reusable agent card for orchestration tests.</summary>
    /// <param name="name">The agent name.</param>
    /// <param name="maxTokenCapacity">The maximum number of tokens the agent can accept.</param>
    /// <param name="trustLevel">The trust level assigned to the agent.</param>
    /// <returns>A configured agent card.</returns>
    private static AgentCard DefaultCard(
        string name = "agent",
        int maxTokenCapacity = DefaultTokenCapacity,
        int trustLevel = DefaultTrustLevel) => new()
        {
            Name = name,
            Capabilities = [ImplementationTaskType],
            Endpoint = "https://agent.example",
            MaxConcurrentGoals = DefaultConcurrentGoalLimit,
            MaxTokenCapacity = maxTokenCapacity,
            SupportedTaskTypes = [ImplementationTaskType],
            Sla = "standard",
            SecurityPosture = TrustedSecurityPosture,
            TrustLevel = trustLevel,
            ToolScopes = [RepositoryReadScope],
            CapacityCacheTtlSeconds = DefaultCapacityCacheTtlSeconds,
        };

    /// <summary>Determines whether every goal has an assigned agent.</summary>
    /// <param name="goals">The goals to inspect.</param>
    /// <returns><see langword="true"/> when every goal has an assigned agent.</returns>
    private static bool AllGoalsAreAssigned(Goal[] goals)
    {
        foreach (var goal in goals)
        {
            if (!goal.AssignedAgentId.HasValue)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether every assigned prompt fits its agent capacity.</summary>
    /// <param name="goals">The goals to inspect.</param>
    /// <param name="agentId">The agent whose assigned prompts are inspected.</param>
    /// <param name="capacity">The maximum permitted prompt length.</param>
    /// <returns><see langword="true"/> when every assigned prompt fits the capacity.</returns>
    private static bool AllAssignedPromptsFit(Goal[] goals, Guid agentId, int capacity)
    {
        foreach (var goal in goals)
        {
            if (goal.AssignedAgentId == agentId && goal.Prompt.Length > capacity)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether every replacement prompt fits a specified capacity.</summary>
    /// <param name="goals">The goals to inspect.</param>
    /// <param name="capacity">The maximum permitted prompt length.</param>
    /// <returns><see langword="true"/> when every prompt fits the capacity.</returns>
    private static bool AllPromptsFit(IReadOnlyList<Goal> goals, int capacity)
    {
        foreach (var goal in goals)
        {
            if (goal.Prompt.Length > capacity)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Deserializes the persisted dependency identifiers for a goal.</summary>
    /// <param name="goal">The goal that contains serialized dependencies.</param>
    /// <returns>The persisted dependency identifiers.</returns>
    private static Guid[] ParseDependencies(Goal goal) => string.IsNullOrWhiteSpace(goal.DependsOnJson)
        ? []
        : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(goal.DependsOnJson) ?? [];

    /// <summary>Owns the in-memory database and service collaborators for one test.</summary>
    private sealed class RaleHarness : IAsyncDisposable
    {
        /// <summary>Maintains the in-memory SQLite database lifetime.</summary>
        private readonly SqliteConnection _connection;

        /// <summary>Initializes a new instance of the <see cref="RaleHarness"/> class.</summary>
        /// <param name="connection">The open SQLite connection.</param>
        /// <param name="contextFactory">The factory for database contexts.</param>
        /// <param name="capacityClient">The controlled capacity client.</param>
        /// <param name="timeProvider">The time source for orchestration services.</param>
        private RaleHarness(
            SqliteConnection connection,
            TestDbContextFactory contextFactory,
            FakeCapacityClient capacityClient,
            TimeProvider timeProvider)
        {
            _connection = connection;
            ContextFactory = contextFactory;
            CapacityClient = capacityClient;
            Orchestrator = new(
                contextFactory,
                capacityClient,
                timeProvider,
                NullLogger<OrchestrationEngineer>.Instance);
        }

        /// <summary>Gets the database context factory used by the harness.</summary>
        public TestDbContextFactory ContextFactory { get; }

        /// <summary>Gets the capacity source used by the harness.</summary>
        public FakeCapacityClient CapacityClient { get; }

        /// <summary>Gets the orchestration service under test.</summary>
        public OrchestrationEngineer Orchestrator { get; }

        /// <summary>Creates and initializes an in-memory orchestration harness.</summary>
        /// <returns>A task that returns the initialized test harness.</returns>
        public static async Task<RaleHarness> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = ":memory:" }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<RALEContext>().UseSqlite(connection).Options;
            var factory = new TestDbContextFactory(options, TimeProvider.System);

            await using var context = factory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();

            return new(connection, factory, new FakeCapacityClient(TimeProvider.System), TimeProvider.System);
        }

        /// <summary>Disposes the database connection.</summary>
        /// <returns>A value task that represents the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    /// <summary>Creates contexts configured with the supplied deterministic time source.</summary>
    /// <param name="options">The configured context options.</param>
    /// <param name="timeProvider">The deterministic time source.</param>
    private sealed class TestDbContextFactory(
        DbContextOptions<RALEContext> options,
        TimeProvider timeProvider) : IDbContextFactory<RALEContext>
    {
        /// <summary>Creates a database context.</summary>
        /// <returns>A configured database context.</returns>
        public RALEContext CreateDbContext() => new(options, timeProvider);
    }

    /// <summary>Provides predictable agent capacity responses for orchestration tests.</summary>
    /// <param name="timeProvider">The time source used for capacity responses.</param>
    private sealed class FakeCapacityClient(TimeProvider timeProvider) : IAgentCapacityClient
    {
        /// <summary>Defines the live capacity response lifetime.</summary>
        private const int LiveCapacityExpiryMinutes = 5;

        /// <summary>Gets capacity responses keyed by agent identifier.</summary>
        public Dictionary<Guid, int> Capacities { get; } = [];

        /// <summary>Gets or sets an explicit response returned before stored capacities.</summary>
        public AgentCapacity? NextCapacity { get; set; }

        /// <summary>Gets the number of capacity queries made.</summary>
        public int QueryCount { get; private set; }

        /// <summary>Returns a configured capacity for an agent.</summary>
        /// <param name="agent">The agent whose capacity is queried.</param>
        /// <param name="taskProfile">The requested task profile.</param>
        /// <param name="cancellationToken">The token used to cancel the query.</param>
        /// <returns>A task that returns the configured capacity, if any.</returns>
        public Task<AgentCapacity?> QueryCapacityAsync(
            Agent agent,
            string taskProfile,
            CancellationToken cancellationToken = default)
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

            var now = timeProvider.GetUtcNow();
            return Task.FromResult<AgentCapacity?>(new(
                agent.Id,
                capacity,
                agent.MaxConcurrentGoals,
                "{}",
                now,
                now.AddMinutes(LiveCapacityExpiryMinutes),
                "live"));
        }
    }
}
