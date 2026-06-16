using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RALE.Server.Data;
using RALE.Server.Models;
using ReactiveUI.Primitives.Signals;

namespace RALE.Server.Services;

public sealed partial class LoopEngineer(
    IDbContextFactory<RALEContext> contextFactory,
    ILogger<LoopEngineer> logger) : ILoopEngineer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<Guid, Signal<Goal>> _goalStreams = new();

    public async Task<Loop> CreateLoop(string primaryPrompt, int tokenLimit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primaryPrompt);

        var drafts = PromptDecomposer.Decompose(primaryPrompt, tokenLimit);
        var loop = new Loop
        {
            PrimaryObjective = primaryPrompt.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = LoopStatus.Running,
            TokenLimit = tokenLimit
        };

        var goals = drafts.Select(draft => new Goal
        {
            LoopId = loop.Id,
            Sequence = draft.Sequence,
            Description = draft.Description,
            Prompt = draft.Prompt,
            Status = GoalStatus.Pending
        }).ToArray();

        for (var i = 1; i < goals.Length; i++)
        {
            goals[i].DependsOnJson = JsonSerializer.Serialize(new[] { goals[i - 1].Id }, JsonOptions);
        }

        loop.Goals.AddRange(goals);
        loop.Events.Add(new LoopEvent
        {
            LoopId = loop.Id,
            Type = LoopEventType.LoopCreated,
            Detail = $"Created loop with {goals.Length} goal(s)."
        });

        foreach (var goal in goals)
        {
            loop.Events.Add(new LoopEvent
            {
                LoopId = loop.Id,
                GoalId = goal.Id,
                Type = LoopEventType.GoalCreated,
                Detail = goal.Description
            });
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Loops.Add(loop);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await EmitReadyGoalsAsync(loop.Id, cancellationToken).ConfigureAwait(false);
        return loop;
    }

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

    public Task<Goal> Decompose(LoopState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var drafts = PromptDecomposer.Decompose(state.PrimaryPrompt, state.TokenLimit);
        if (state.ExistingGoalCount >= drafts.Count)
        {
            throw new InvalidOperationException("The loop state has no remaining prompt material to decompose.");
        }

        var draft = drafts[state.ExistingGoalCount];
        var goal = new Goal
        {
            LoopId = state.LoopId,
            Sequence = draft.Sequence,
            Description = draft.Description,
            Prompt = draft.Prompt,
            Status = GoalStatus.Pending
        };

        return Task.FromResult(goal);
    }

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
        result.CompletedAt = result.CompletedAt == default ? DateTimeOffset.UtcNow : result.CompletedAt;
        result.Metadata = string.IsNullOrWhiteSpace(result.Metadata) ? "{}" : result.Metadata;

        context.GoalResults.Add(result);
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

        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = LoopEventType.GoalCompleted,
            Detail = $"Goal {goal.Sequence} completed."
        });

        await ReduceLoopStatusAsync(context, goal.LoopId, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await EmitReadyGoalsAsync(goal.LoopId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Loop?> GetLoopAsync(Guid loopId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Loops
            .AsNoTracking()
            .Include(loop => loop.Goals.OrderBy(goal => goal.Sequence))
            .FirstOrDefaultAsync(loop => loop.Id == loopId, cancellationToken)
            .ConfigureAwait(false);
    }

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
        goal.StartedAt = DateTimeOffset.UtcNow;
        goal.Version++;

        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = LoopEventType.GoalClaimed,
            Detail = $"Goal {goal.Sequence} claimed for execution."
        });

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
        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = LoopEventType.GoalPaused,
            Detail = $"Goal {goal.Sequence} paused."
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

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

        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = LoopEventType.GoalResumed,
            Detail = $"Goal {goal.Sequence} resumed."
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await EmitReadyGoalsAsync(goal.LoopId, cancellationToken).ConfigureAwait(false);
        return goal;
    }

    public async Task<GoalResult> CompleteGoalAsync(Guid goalId, string output, string? metadata = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException("Goal output is required.", nameof(output));
        }

        var result = new GoalResult
        {
            GoalId = goalId,
            Output = output.Trim(),
            Metadata = string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata.Trim(),
            CompletedAt = DateTimeOffset.UtcNow
        };

        await UpdateWithResult(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

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

        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = LoopEventType.GoalFailed,
            Detail = string.IsNullOrWhiteSpace(reason) ? "Goal failed." : reason.Trim()
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

    private async Task EmitReadyGoalsAsync(Guid loopId, CancellationToken cancellationToken)
    {
        var stream = GetStream(loopId);
        foreach (var goal in await ListReadyGoalsAsync(loopId, cancellationToken).ConfigureAwait(false))
        {
            stream.OnNext(goal);
        }
    }

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

        var statusById = loop.Goals.ToDictionary(goal => goal.Id, goal => goal.Status);
        return [.. loop.Goals
            .Where(goal => goal.Status == GoalStatus.Pending && DependenciesComplete(goal, statusById))
            .OrderBy(goal => goal.Sequence)];
    }

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

    private static Guid[] ParseDependencies(Goal goal)
    {
        if (string.IsNullOrWhiteSpace(goal.DependsOnJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Guid[]>(goal.DependsOnJson, JsonOptions) ?? [];
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Goal {GoalId} claim lost an optimistic concurrency race.")]
    private static partial void GoalClaimLost(ILogger logger, Guid goalId);

    private static async Task ReduceLoopStatusAsync(RALEContext context, Guid loopId, CancellationToken cancellationToken)
    {
        var goals = await context.Goals
            .Where(goal => goal.LoopId == loopId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (goals.Length == 0 || goals.Any(goal => goal.Status != GoalStatus.Complete))
        {
            return;
        }

        var loop = await context.Loops.FirstAsync(existing => existing.Id == loopId, cancellationToken).ConfigureAwait(false);
        if (loop.Status == LoopStatus.Complete)
        {
            return;
        }

        loop.Status = LoopStatus.Complete;
        loop.Version++;
        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = loopId,
            Type = LoopEventType.LoopCompleted,
            Detail = "All goals completed."
        });
    }

    private Signal<Goal> GetStream(Guid loopId) => _goalStreams.GetOrAdd(loopId, _ => new Signal<Goal>());
}
