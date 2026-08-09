// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Invokes the tool that performs work for a goal.</summary>
public interface IAgentToolClient
{
    /// <summary>Executes the supplied goal.</summary>
    /// <param name="goal">The goal to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent execution result.</returns>
    Task<AgentExecutionResult> ExecuteAsync(Goal goal, CancellationToken cancellationToken);
}
