// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Executes a claimed goal through an agent tool client.</summary>
public interface IAgentExecutor
{
    /// <summary>Claims and executes a goal.</summary>
    /// <param name="goal">The goal to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed result, or <see langword="null"/> when the goal could not be claimed.</returns>
    Task<GoalResult?> Execute(Goal goal, CancellationToken cancellationToken);
}
