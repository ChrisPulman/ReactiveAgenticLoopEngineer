using System.Text.Json;
using Microsoft.Extensions.Logging;
using RALE.Server.Models;

namespace RALE.Server.Services;

public interface IAgentExecutor
{
    Task<GoalResult?> Execute(Goal goal, CancellationToken cancellationToken = default);
}

public interface IAgentToolClient
{
    Task<AgentExecutionResult> ExecuteAsync(Goal goal, CancellationToken cancellationToken = default);
}

public sealed record AgentExecutionResult(string Output, string Metadata);

public sealed class DeterministicAgentToolClient : IAgentToolClient
{
    public Task<AgentExecutionResult> ExecuteAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var metadata = JsonSerializer.Serialize(new
        {
            executor = nameof(DeterministicAgentToolClient),
            promptLength = goal.Prompt.Length
        });

        return Task.FromResult(new AgentExecutionResult($"Executed goal {goal.Sequence}: {goal.Description}", metadata));
    }
}

public sealed partial class AgentExecutor(
    ILoopEngineer loopEngineer,
    IAgentToolClient toolClient,
    ILogger<AgentExecutor> logger) : IAgentExecutor
{
    public async Task<GoalResult?> Execute(Goal goal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        if (!await loopEngineer.TryClaimGoalAsync(goal.Id, cancellationToken).ConfigureAwait(false))
        {
            GoalNotClaimed(logger, goal.Id);
            return null;
        }

        try
        {
            var execution = await toolClient.ExecuteAsync(goal, cancellationToken).ConfigureAwait(false);
            return await loopEngineer
                .CompleteGoalAsync(goal.Id, execution.Output, execution.Metadata, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await loopEngineer.FailGoalAsync(goal.Id, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Goal {GoalId} was not claimed; another executor may already own it.")]
    private static partial void GoalNotClaimed(ILogger logger, Guid goalId);
}
