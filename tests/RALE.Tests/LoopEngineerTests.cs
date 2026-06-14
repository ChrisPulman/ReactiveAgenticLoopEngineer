using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RALE.Server.Data;
using RALE.Server.Models;
using RALE.Server.Services;

namespace RALE.Tests;

public sealed class LoopEngineerTests
{
    [Test]
    public async Task CreateLoop_persists_ordered_goals_and_primes_observer()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop("first step second step third step fourth step", 16);
        var observed = new TaskCompletionSource<Goal>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = harness.Engineer.ObserveNextGoals(loop.Id).Subscribe(new ActionObserver<Goal>(goal => observed.TrySetResult(goal)));
        var emitted = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.That(loop.Goals).Count().IsEqualTo(4);
        await Assert.That(loop.Goals.All(goal => goal.Prompt.Length <= 16)).IsTrue();
        await Assert.That(emitted.Sequence).IsEqualTo(1);
    }

    [Test]
    public async Task ClaimNextGoal_allows_only_one_executor_to_claim_goal()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop("single goal", 100);
        var goal = loop.Goals.Single();

        var claims = await Task.WhenAll(
            harness.Engineer.TryClaimGoalAsync(goal.Id),
            harness.Engineer.TryClaimGoalAsync(goal.Id));

        await Assert.That(claims.Count(claim => claim)).IsEqualTo(1);
    }

    [Test]
    public async Task CompleteGoal_reduces_loop_to_complete_when_final_goal_finishes()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop("single goal", 100);
        var goal = loop.Goals.Single();

        await harness.Engineer.TryClaimGoalAsync(goal.Id);
        await harness.Engineer.CompleteGoalAsync(goal.Id, "done");

        var reloaded = await harness.Engineer.GetLoopAsync(loop.Id);

        await Assert.That(reloaded).IsNotNull();
        await Assert.That(reloaded!.Status).IsEqualTo(LoopStatus.Complete);
        await Assert.That(reloaded.Goals.Single().Status).IsEqualTo(GoalStatus.Complete);
    }

    [Test]
    public async Task Claim_and_complete_runs_multi_goal_loop_to_completion_in_order()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop("alpha beta gamma delta epsilon zeta", 12);
        var completedSequences = new List<int>();

        while (await harness.Engineer.ClaimNextGoalAsync(loop.Id) is { } goal)
        {
            completedSequences.Add(goal.Sequence);
            await harness.Engineer.CompleteGoalAsync(goal.Id, $"completed {goal.Sequence}");
        }

        var reloaded = await harness.Engineer.GetLoopAsync(loop.Id);

        await Assert.That(completedSequences.Count > 1).IsTrue();
        await Assert.That(completedSequences.SequenceEqual(Enumerable.Range(1, completedSequences.Count))).IsTrue();
        await Assert.That(reloaded).IsNotNull();
        await Assert.That(reloaded!.Status).IsEqualTo(LoopStatus.Complete);
        await Assert.That(reloaded.Goals.All(goal => goal.Status == GoalStatus.Complete)).IsTrue();
    }

    [Test]
    public async Task Pause_and_resume_goal_returns_goal_to_ready_pipeline()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop("single goal", 100);
        var goal = loop.Goals.Single();

        var paused = await harness.Engineer.PauseGoalAsync(goal.Id);
        var resumed = await harness.Engineer.ResumeGoalAsync(goal.Id);

        await Assert.That(paused!.Status).IsEqualTo(GoalStatus.Paused);
        await Assert.That(resumed!.Status).IsEqualTo(GoalStatus.Pending);
    }

    private sealed class RaleHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RaleHarness(SqliteConnection connection, TestDbContextFactory contextFactory)
        {
            _connection = connection;
            ContextFactory = contextFactory;
            Engineer = new LoopEngineer(contextFactory, NullLogger<LoopEngineer>.Instance);
        }

        public TestDbContextFactory ContextFactory { get; }

        public LoopEngineer Engineer { get; }

        public static async Task<RaleHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<RALEContext>().UseSqlite(connection).Options;
            var factory = new TestDbContextFactory(options);

            await using var context = factory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();

            return new RaleHarness(connection, factory);
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<RALEContext> options) : IDbContextFactory<RALEContext>
    {
        public RALEContext CreateDbContext() => new(options);
    }

    private sealed class ActionObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(T value) => onNext(value);
    }
}
