// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Coordinates goal claims with tool execution and completion recording.</summary>
/// <param name="loopEngineer">The persisted loop lifecycle service.</param>
/// <param name="toolClient">The client that executes claimed goal work.</param>
/// <param name="logger">The logger used for claim diagnostics.</param>
public sealed partial class AgentExecutor(
    ILoopEngineer loopEngineer,
    IAgentToolClient toolClient,
    ILogger<AgentExecutor> logger) : IAgentExecutor
{
    /// <inheritdoc />
    public async Task<GoalResult?> Execute(Goal goal, CancellationToken cancellationToken)
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
            _ = await loopEngineer.FailGoalAsync(goal.Id, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Goal {GoalId} was not claimed; another executor may already own it.")]
    private static partial void GoalNotClaimed(ILogger logger, Guid goalId);
}
