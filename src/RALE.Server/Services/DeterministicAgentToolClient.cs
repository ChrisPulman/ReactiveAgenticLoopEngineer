// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Provides deterministic execution for local and test workflows.</summary>
public sealed class DeterministicAgentToolClient : IAgentToolClient
{
    /// <inheritdoc />
    public Task<AgentExecutionResult> ExecuteAsync(Goal goal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(goal);
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = JsonSerializer.Serialize(new
        {
            executor = nameof(DeterministicAgentToolClient),
            promptLength = goal.Prompt.Length
        });

        return Task.FromResult(new AgentExecutionResult($"Executed goal {goal.Sequence}: {goal.Description}", metadata));
    }
}
