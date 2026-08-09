// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RALE.Server.Data;
using RALE.Server.Models;
using ReactiveUI.Primitives.Signals;

namespace RALE.Server.Services;

/// <summary>Persists and coordinates the lifecycle of decomposed goal loops.</summary>
/// <param name="contextFactory">The database context factory.</param>
/// <param name="timeProvider">The provider for current timestamps.</param>
/// <param name="logger">The logger for loop lifecycle diagnostics.</param>
public sealed partial class LoopEngineer(
    IDbContextFactory<RALEContext> contextFactory,
    TimeProvider timeProvider,
    ILogger<LoopEngineer> logger) : ILoopEngineer
{
    /// <summary>The shared JSON serializer configuration.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The observable goal streams keyed by loop identifier.</summary>
    private readonly ConcurrentDictionary<Guid, Signal<Goal>> _goalStreams = new();

    /// <inheritdoc />
    public async Task<Loop> CreateLoop(string primaryPrompt, int tokenLimit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primaryPrompt);

        var drafts = PromptDecomposer.Decompose(primaryPrompt, tokenLimit);
        var loop = new Loop { PrimaryObjective = primaryPrompt.Trim(), CreatedAt = timeProvider.GetUtcNow(), Status = LoopStatus.Running, TokenLimit = tokenLimit };

        var goals = new Goal[drafts.Count];
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            goals[index] = new Goal { LoopId = loop.Id, Sequence = draft.Sequence, Description = draft.Description, Prompt = draft.Prompt, Status = GoalStatus.Pending };
        }

        for (var i = 1; i < goals.Length; i++)
        {
            goals[i].DependsOnJson = JsonSerializer.Serialize(new[] { goals[i - 1].Id }, JsonOptions);
        }

        loop.Goals.AddRange(goals);
        loop.Events.Add(new LoopEvent { LoopId = loop.Id, Type = LoopEventType.LoopCreated, Detail = $"Created loop with {goals.Length} goal(s)." });

        foreach (var goal in goals)
        {
            loop.Events.Add(new LoopEvent { LoopId = loop.Id, GoalId = goal.Id, Type = LoopEventType.GoalCreated, Detail = goal.Description });
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        _ = await context.Loops.AddAsync(loop, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await EmitReadyGoalsAsync(loop.Id, cancellationToken).ConfigureAwait(false);
        return loop;
    }

    /// <inheritdoc />
    public IObservable<Goal> ObserveNextGoals(Guid loopId)
    {
        var stream = GetStream(loopId);

        return Signal.CreateSafe<Goal>(observer =>
        {
            var subscription = stream.Subscribe(observer);
            _ = Task.Run(async () =>
            {
                try
                {
                    foreach (var goal in await ListReadyGoalsAsync(loopId, CancellationToken.None).ConfigureAwait(false))
                    {
                        observer.OnNext(goal);
                    }
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            });

            return subscription;
        });
    }

    /// <inheritdoc />
    public Task<Goal> Decompose(LoopState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var drafts = PromptDecomposer.Decompose(state.PrimaryPrompt, state.TokenLimit);
        if (state.ExistingGoalCount >= drafts.Count)
        {
            throw new InvalidOperationException("The loop state has no remaining prompt material to decompose.");
        }

        var draft = drafts[state.ExistingGoalCount];
        var goal = new Goal { LoopId = state.LoopId, Sequence = draft.Sequence, Description = draft.Description, Prompt = draft.Prompt, Status = GoalStatus.Pending };

        return Task.FromResult(goal);
    }

    /// <inheritdoc />
    public async Task UpdateWithResult(GoalResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.GoalId == Guid.Empty)
        {
            throw new ArgumentException("Goal result must include a goal id.", nameof(result));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals
            .Include(existing => existing.Loop)
            .FirstOrDefaultAsync(existing => existing.Id == result.GoalId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Goal '{result.GoalId}' was not found.");

        if (goal.Status == GoalStatus.Complete)
        {
            return;
        }

        result.Id = result.Id == Guid.Empty ? Guid.NewGuid() : result.Id;
        result.CompletedAt = result.CompletedAt == default ? timeProvider.GetUtcNow() : result.CompletedAt;
        result.Metadata = string.IsNullOrWhiteSpace(result.Metadata) ? "{}" : result.Metadata;

        _ = await context.GoalResults.AddAsync(result, cancellationToken).ConfigureAwait(false);
        goal.Status = GoalStatus.Complete;
        goal.CompletedAt = result.CompletedAt;
        goal.Version++;

        if (goal.AssignedAgentId.HasValue)
        {
            var agent = await context.Agents
                .FirstOrDefaultAsync(existing => existing.Id == goal.AssignedAgentId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (agent is not null)
            {
                agent.CurrentLoad = Math.Max(0, agent.CurrentLoad - 1);
                if (agent.AssignedGoalId == goal.Id)
                {
                    agent.AssignedGoalId = null;
                }

                agent.Version++;
            }
        }

        _ = await context.LoopEvents.AddAsync(
            new LoopEvent { LoopId = goal.LoopId, GoalId = goal.Id, Type = LoopEventType.GoalCompleted, Detail = $"Goal {goal.Sequence} completed." },
            cancellationToken).ConfigureAwait(false);

        await ReduceLoopStatusAsync(context, goal.LoopId, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await EmitReadyGoalsAsync(goal.LoopId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Loop?> GetLoopAsync(Guid loopId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var loop = await context.Loops
            .AsNoTracking()
            .Include(static loop => loop.Goals)
            .FirstOrDefaultAsync(loop => loop.Id == loopId, cancellationToken)
            .ConfigureAwait(false);

        loop?.Goals.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
        return loop;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Goal>> ListGoalsAsync(Guid loopId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Goals
            .AsNoTracking()
            .Where(goal => goal.LoopId == loopId)
            .OrderBy(goal => goal.Sequence)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Goal?> ClaimNextGoalAsync(Guid loopId, CancellationToken cancellationToken = default)
    {
        foreach (var goal in await ListReadyGoalsAsync(loopId, cancellationToken).ConfigureAwait(false))
        {
            if (await TryClaimGoalAsync(goal.Id, cancellationToken).ConfigureAwait(false))
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                return await context.Goals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(existing => existing.Id == goal.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> TryClaimGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals
            .Include(existing => existing.Loop)
            .FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken)
            .ConfigureAwait(false);

        if (goal is null || goal.Status != GoalStatus.Pending || goal.Loop.Status != LoopStatus.Running)
        {
            return false;
        }

        if (!await DependenciesCompleteAsync(context, goal, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        goal.Status = GoalStatus.InProgress;
        goal.StartedAt = timeProvider.GetUtcNow();
        goal.Version++;

        _ = await context.LoopEvents.AddAsync(
            new LoopEvent { LoopId = goal.LoopId, GoalId = goal.Id, Type = LoopEventType.GoalClaimed, Detail = $"Goal {goal.Sequence} claimed for execution." },
            cancellationToken).ConfigureAwait(false);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            GoalClaimLost(logger, goalId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Goal?> PauseGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals.FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken).ConfigureAwait(false);
        if (goal is null || goal.Status is GoalStatus.Complete or GoalStatus.Failed or GoalStatus.Skipped)
        {
            return goal;
        }

        goal.Status = GoalStatus.Paused;
        goal.Version++;
        _ = await context.LoopEvents.AddAsync(
            new LoopEvent { LoopId = goal.LoopId, GoalId = goal.Id, Type = LoopEventType.GoalPaused, Detail = $"Goal {goal.Sequence} paused." },
            cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

    /// <inheritdoc />
    public async Task<Goal?> ResumeGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals
            .Include(existing => existing.Loop)
            .FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken)
            .ConfigureAwait(false);

        if (goal is null || goal.Status != GoalStatus.Paused)
        {
            return goal;
        }

        goal.Status = GoalStatus.Pending;
        goal.Version++;

        if (goal.Loop.Status == LoopStatus.Paused)
        {
            goal.Loop.Status = LoopStatus.Running;
            goal.Loop.Version++;
        }

        _ = await context.LoopEvents.AddAsync(
            new LoopEvent { LoopId = goal.LoopId, GoalId = goal.Id, Type = LoopEventType.GoalResumed, Detail = $"Goal {goal.Sequence} resumed." },
            cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await EmitReadyGoalsAsync(goal.LoopId, cancellationToken).ConfigureAwait(false);
        return goal;
    }

    /// <inheritdoc />
    public async Task<GoalResult> CompleteGoalAsync(Guid goalId, string output, string? metadata = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(output);

        var result = new GoalResult { GoalId = goalId, Output = output.Trim(), Metadata = string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata.Trim(), CompletedAt = timeProvider.GetUtcNow() };

        await UpdateWithResult(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Goal?> FailGoalAsync(Guid goalId, string reason, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals
            .Include(existing => existing.Loop)
            .FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken)
            .ConfigureAwait(false);

        if (goal is null || goal.Status == GoalStatus.Complete)
        {
            return goal;
        }

        goal.Status = GoalStatus.Failed;
        goal.Version++;
        goal.Loop.Status = LoopStatus.Failed;
        goal.Loop.Version++;

        _ = await context.LoopEvents.AddAsync(
            new LoopEvent { LoopId = goal.LoopId, GoalId = goal.Id, Type = LoopEventType.GoalFailed, Detail = string.IsNullOrWhiteSpace(reason) ? "Goal failed." : reason.Trim() },
            cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

    /// <summary>Logs an optimistic concurrency claim failure.</summary>
    /// <param name="logger">The logger receiving the diagnostic.</param>
    /// <param name="goalId">The goal identifier.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Goal {GoalId} claim lost an optimistic concurrency race.")]
    private static partial void GoalClaimLost(ILogger logger, Guid goalId);

    /// <summary>Determines whether all persisted dependencies have completed.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="goal">The goal whose dependencies are checked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when every dependency is complete.</returns>
    private static async Task<bool> DependenciesCompleteAsync(RALEContext context, Goal goal, CancellationToken cancellationToken)
    {
        var dependencies = ParseDependencies(goal);
        if (dependencies.Length == 0)
        {
            return true;
        }

        var completedCount = await context.Goals
            .AsNoTracking()
            .CountAsync(existing => dependencies.Contains(existing.Id) && existing.Status == GoalStatus.Complete, cancellationToken)
            .ConfigureAwait(false);

        return completedCount == dependencies.Length;
    }

    /// <summary>Determines whether all in-memory dependencies have completed.</summary>
    /// <param name="goal">The goal whose dependencies are checked.</param>
    /// <param name="statusById">The goal statuses keyed by identifier.</param>
    /// <returns><see langword="true"/> when every dependency is complete.</returns>
    private static bool DependenciesComplete(Goal goal, Dictionary<Guid, GoalStatus> statusById)
    {
        foreach (var dependency in ParseDependencies(goal))
        {
            if (!statusById.TryGetValue(dependency, out var status) || status != GoalStatus.Complete)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Parses the dependency identifiers stored on a goal.</summary>
    /// <param name="goal">The goal containing serialized dependencies.</param>
    /// <returns>The dependency identifiers.</returns>
    private static Guid[] ParseDependencies(Goal goal) => string.IsNullOrWhiteSpace(goal.DependsOnJson)
        ? []
        : JsonSerializer.Deserialize<Guid[]>(goal.DependsOnJson, JsonOptions) ?? [];

    /// <summary>Marks a loop complete when every goal has completed.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="loopId">The loop identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the status reduction operation.</returns>
    private static async Task ReduceLoopStatusAsync(RALEContext context, Guid loopId, CancellationToken cancellationToken)
    {
        var goals = await context.Goals
            .Where(goal => goal.LoopId == loopId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var goal in goals)
        {
            if (goal.Status != GoalStatus.Complete)
            {
                return;
            }
        }

        var loop = await context.Loops.FirstAsync(existing => existing.Id == loopId, cancellationToken).ConfigureAwait(false);
        if (loop.Status == LoopStatus.Complete)
        {
            return;
        }

        loop.Status = LoopStatus.Complete;
        loop.Version++;
        _ = await context.LoopEvents.AddAsync(
            new LoopEvent { LoopId = loopId, Type = LoopEventType.LoopCompleted, Detail = "All goals completed." },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Publishes all currently executable goals for a loop.</summary>
    /// <param name="loopId">The loop identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    private async Task EmitReadyGoalsAsync(Guid loopId, CancellationToken cancellationToken)
    {
        var stream = GetStream(loopId);
        foreach (var goal in await ListReadyGoalsAsync(loopId, cancellationToken).ConfigureAwait(false))
        {
            stream.OnNext(goal);
        }
    }

    /// <summary>Lists pending goals whose dependencies are complete.</summary>
    /// <param name="loopId">The loop identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ready goals.</returns>
    private async Task<IReadOnlyList<Goal>> ListReadyGoalsAsync(Guid loopId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var loop = await context.Loops
            .AsNoTracking()
            .Include(existing => existing.Goals)
            .FirstOrDefaultAsync(existing => existing.Id == loopId, cancellationToken)
            .ConfigureAwait(false);

        if (loop is null || loop.Status != LoopStatus.Running)
        {
            return [];
        }

        var statusById = new Dictionary<Guid, GoalStatus>(loop.Goals.Count);
        foreach (var goal in loop.Goals)
        {
            statusById.Add(goal.Id, goal.Status);
        }

        var readyGoals = new List<Goal>();
        foreach (var goal in loop.Goals)
        {
            if (goal.Status == GoalStatus.Pending && DependenciesComplete(goal, statusById))
            {
                readyGoals.Add(goal);
            }
        }

        readyGoals.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
        return readyGoals;
    }

    /// <summary>Retrieves or creates the signal stream for a loop.</summary>
    /// <param name="loopId">The loop identifier.</param>
    /// <returns>The signal stream.</returns>
    private Signal<Goal> GetStream(Guid loopId) => _goalStreams.GetOrAdd(loopId, static _ => new Signal<Goal>());
}
