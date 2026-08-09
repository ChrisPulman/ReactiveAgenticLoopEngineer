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

/// <summary>Verifies persisted loop lifecycle operations.</summary>
public sealed class LoopEngineerTests
{
    /// <summary>The number of goals expected from the token-limited prompt.</summary>
    private const int ExpectedGoalCount = 4;

    /// <summary>The sequence number assigned to the first goal.</summary>
    private const int InitialGoalSequence = 1;

    /// <summary>The token limit used to produce a multi-goal loop.</summary>
    private const int ShortTokenLimit = 12;

    /// <summary>The standard token limit used by single-goal tests.</summary>
    private const int StandardTokenLimit = 100;

    /// <summary>The token limit used to split the initial test prompt.</summary>
    private const int TokenLimitedPromptLength = 16;

    /// <summary>The prompt used to create exactly one goal.</summary>
    private const string SingleGoalPrompt = "single goal";

    /// <summary>Verifies that creating a loop persists ordered goals and emits the first goal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateLoop_persists_ordered_goals_and_primes_observer()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop("first step second step third step fourth step", TokenLimitedPromptLength);
        var observed = new TaskCompletionSource<Goal>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = harness.Engineer.ObserveNextGoals(loop.Id).Subscribe(new ActionObserver<Goal>(goal => observed.TrySetResult(goal)));
        var emitted = await observed.Task.WaitAsync(TimeSpan.FromSeconds(ExpectedGoalCount));

        await Assert.That(loop.Goals.Count).IsEqualTo(ExpectedGoalCount);
        await Assert.That(AllPromptsAreWithinLimit(loop.Goals, TokenLimitedPromptLength)).IsTrue();
        await Assert.That(emitted.Sequence).IsEqualTo(InitialGoalSequence);
    }

    /// <summary>Verifies that a goal can be claimed by only one concurrent executor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClaimNextGoal_allows_only_one_executor_to_claim_goal()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop(SingleGoalPrompt, StandardTokenLimit);
        var goal = GetOnlyGoal(loop.Goals);

        var claims = await Task.WhenAll(
            harness.Engineer.TryClaimGoalAsync(goal.Id),
            harness.Engineer.TryClaimGoalAsync(goal.Id));

        await Assert.That(CountSuccessfulClaims(claims)).IsEqualTo(InitialGoalSequence);
    }

    /// <summary>Verifies that completion of the final goal completes its loop.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompleteGoal_reduces_loop_to_complete_when_final_goal_finishes()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop(SingleGoalPrompt, StandardTokenLimit);
        var goal = GetOnlyGoal(loop.Goals);

        await harness.Engineer.TryClaimGoalAsync(goal.Id);
        await harness.Engineer.CompleteGoalAsync(goal.Id, "done");

        var reloaded = await harness.Engineer.GetLoopAsync(loop.Id);

        await Assert.That(reloaded).IsNotNull();
        await Assert.That(reloaded!.Status).IsEqualTo(LoopStatus.Complete);
        await Assert.That(GetOnlyGoal(reloaded.Goals).Status).IsEqualTo(GoalStatus.Complete);
    }

    /// <summary>Verifies that claims and completions finish a multi-goal loop in sequence order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Claim_and_complete_runs_multi_goal_loop_to_completion_in_order()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop("alpha beta gamma delta epsilon zeta", ShortTokenLimit);
        var completedSequences = new List<int>();

        while (await harness.Engineer.ClaimNextGoalAsync(loop.Id) is { } goal)
        {
            completedSequences.Add(goal.Sequence);
            await harness.Engineer.CompleteGoalAsync(goal.Id, $"completed {goal.Sequence}");
        }

        var reloaded = await harness.Engineer.GetLoopAsync(loop.Id);

        await Assert.That(completedSequences.Count > InitialGoalSequence).IsTrue();
        await Assert.That(IsSequentialFromInitialGoal(completedSequences)).IsTrue();
        await Assert.That(reloaded).IsNotNull();
        await Assert.That(reloaded!.Status).IsEqualTo(LoopStatus.Complete);
        await Assert.That(AllGoalsAreComplete(reloaded.Goals)).IsTrue();
    }

    /// <summary>Verifies that a paused goal can resume in the ready pipeline.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Pause_and_resume_goal_returns_goal_to_ready_pipeline()
    {
        await using var harness = await RaleHarness.CreateAsync();
        var loop = await harness.Engineer.CreateLoop(SingleGoalPrompt, StandardTokenLimit);
        var goal = GetOnlyGoal(loop.Goals);

        var paused = await harness.Engineer.PauseGoalAsync(goal.Id);
        var resumed = await harness.Engineer.ResumeGoalAsync(goal.Id);

        await Assert.That(paused!.Status).IsEqualTo(GoalStatus.Paused);
        await Assert.That(resumed!.Status).IsEqualTo(GoalStatus.Pending);
    }

    /// <summary>Determines whether every prompt is within the supplied token limit.</summary>
    /// <param name="goals">The goals whose prompts are evaluated.</param>
    /// <param name="tokenLimit">The maximum permitted prompt length.</param>
    /// <returns><see langword="true"/> when every prompt is within the limit; otherwise, <see langword="false"/>.</returns>
    private static bool AllPromptsAreWithinLimit(IEnumerable<Goal> goals, int tokenLimit)
    {
        foreach (var goal in goals)
        {
            if (goal.Prompt.Length > tokenLimit)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Counts successful claim results.</summary>
    /// <param name="claims">The claim results to count.</param>
    /// <returns>The number of successful claims.</returns>
    private static int CountSuccessfulClaims(IEnumerable<bool> claims)
    {
        var successfulClaims = 0;
        foreach (var claim in claims)
        {
            if (claim)
            {
                successfulClaims++;
            }
        }

        return successfulClaims;
    }

    /// <summary>Determines whether completed sequence values begin at one and remain contiguous.</summary>
    /// <param name="sequences">The sequence values to evaluate.</param>
    /// <returns><see langword="true"/> when the values are contiguous; otherwise, <see langword="false"/>.</returns>
    private static bool IsSequentialFromInitialGoal(List<int> sequences)
    {
        for (var index = 0; index < sequences.Count; index++)
        {
            if (sequences[index] != index + InitialGoalSequence)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether every supplied goal is complete.</summary>
    /// <param name="goals">The goals to evaluate.</param>
    /// <returns><see langword="true"/> when every goal is complete; otherwise, <see langword="false"/>.</returns>
    private static bool AllGoalsAreComplete(IEnumerable<Goal> goals)
    {
        foreach (var goal in goals)
        {
            if (goal.Status != GoalStatus.Complete)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the sole goal in a collection without using LINQ.</summary>
    /// <param name="goals">The collection expected to contain one goal.</param>
    /// <returns>The only goal in the collection.</returns>
    /// <exception cref="InvalidOperationException">The collection contains zero or more than one goal.</exception>
    private static Goal GetOnlyGoal(IEnumerable<Goal> goals)
    {
        using var enumerator = goals.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new InvalidOperationException("The collection does not contain a goal.");
        }

        var goal = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException("The collection contains more than one goal.");
        }

        return goal;
    }

    /// <summary>Owns an isolated in-memory database and its loop engineer.</summary>
    private sealed class RaleHarness : IAsyncDisposable
    {
        /// <summary>The open connection that keeps the in-memory database alive.</summary>
        private readonly SqliteConnection _connection;

        /// <summary>Initializes a new instance of the <see cref="RaleHarness"/> class.</summary>
        /// <param name="connection">The open connection that owns the in-memory database.</param>
        /// <param name="contextFactory">The factory that creates contexts for the database.</param>
        private RaleHarness(SqliteConnection connection, TestDbContextFactory contextFactory)
        {
            _connection = connection;
            ContextFactory = contextFactory;
            Engineer = new(contextFactory, TimeProvider.System, NullLogger<LoopEngineer>.Instance);
        }

        /// <summary>Gets the factory for contexts backed by the isolated database.</summary>
        public TestDbContextFactory ContextFactory { get; }

        /// <summary>Gets the loop lifecycle service under test.</summary>
        public LoopEngineer Engineer { get; }

        /// <summary>Creates and initializes an isolated in-memory test database.</summary>
        /// <returns>A task whose result is an initialized test harness.</returns>
        public static async Task<RaleHarness> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = ":memory:" }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<RALEContext>().UseSqlite(connection).Options;
            var factory = new TestDbContextFactory(options);

            await using var context = factory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();

            return new(connection, factory);
        }

        /// <summary>Disposes the open database connection.</summary>
        /// <returns>A value task that represents the asynchronous disposal operation.</returns>
        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    /// <summary>Creates contexts from the supplied test database options.</summary>
    private sealed class TestDbContextFactory : IDbContextFactory<RALEContext>
    {
        /// <summary>The options shared by each test context.</summary>
        private readonly DbContextOptions<RALEContext> _options;

        /// <summary>Initializes a new instance of the <see cref="TestDbContextFactory"/> class.</summary>
        /// <param name="options">The options shared by created contexts.</param>
        public TestDbContextFactory(DbContextOptions<RALEContext> options) => _options = options;

        /// <summary>Creates a context backed by the test database.</summary>
        /// <returns>A context backed by the test database.</returns>
        public RALEContext CreateDbContext() => new(_options, TimeProvider.System);
    }

    /// <summary>Forwards observable values to a supplied action.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class ActionObserver<T> : IObserver<T>
    {
        /// <summary>The action invoked for each value.</summary>
        private readonly Action<T> _onNext;

        /// <summary>Initializes a new instance of the <see cref="ActionObserver{T}"/> class.</summary>
        /// <param name="onNext">The action invoked for each observed value.</param>
        public ActionObserver(Action<T> onNext) => _onNext = onNext;

        /// <summary>Handles successful observable completion.</summary>
        public void OnCompleted()
        {
        }

        /// <summary>Propagates an observable error.</summary>
        /// <param name="error">The error raised by the observable.</param>
        public void OnError(Exception error) => throw error;

        /// <summary>Forwards an observable value.</summary>
        /// <param name="value">The value raised by the observable.</param>
        public void OnNext(T value) => _onNext(value);
    }
}
