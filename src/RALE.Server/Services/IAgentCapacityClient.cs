// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Obtains capacity observations from a registered execution agent.</summary>
public interface IAgentCapacityClient
{
    /// <summary>Queries the capacity of an execution agent for a task profile.</summary>
    /// <param name="agent">The agent to query.</param>
    /// <param name="taskProfile">The requested task profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The observed capacity, or <see langword="null"/> when unavailable.</returns>
    Task<AgentCapacity?> QueryCapacityAsync(Agent agent, string taskProfile, CancellationToken cancellationToken);
}
