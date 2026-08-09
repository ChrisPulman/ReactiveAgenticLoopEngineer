// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Coordinates persisted multi-agent planning and goal execution.</summary>
public interface IOrchestrationEngineer
{
    /// <summary>Registers an execution agent.</summary>
    /// <param name="card">The agent registration profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registered agent.</returns>
    Task<Agent> RegisterAgentAsync(AgentCard card, CancellationToken cancellationToken);

    /// <summary>Lists the registered agents.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registered agents.</returns>
    Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken cancellationToken);

    /// <summary>Discovers the capacity of a registered agent.</summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="taskProfile">The requested task profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The discovered capacity.</returns>
    Task<AgentCapacity> DiscoverCapacityAsync(Guid agentId, string taskProfile, CancellationToken cancellationToken);

    /// <summary>Creates a persisted master plan.</summary>
    /// <param name="request">The master-plan request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created loop.</returns>
    Task<Loop> CreateMasterPlanAsync(MasterPlanRequest request, CancellationToken cancellationToken);

    /// <summary>Assigns the next eligible goal to an agent.</summary>
    /// <param name="loopId">The loop identifier.</param>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assigned goal, or <see langword="null"/> when none is eligible.</returns>
    Task<Goal?> AssignNextGoalAsync(Guid loopId, Guid agentId, CancellationToken cancellationToken);

    /// <summary>Records a reviewer decision for a goal.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="approved">Whether the goal is approved.</param>
    /// <param name="reviewer">The reviewer identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated goal, or <see langword="null"/> when it does not exist.</returns>
    Task<Goal?> ApproveGoalAsync(Guid goalId, bool approved, string reviewer, CancellationToken cancellationToken);

    /// <summary>Records execution progress for a goal.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="detail">The progress detail.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated goal, or <see langword="null"/> when it does not exist.</returns>
    Task<Goal?> RecordHeartbeatAsync(Guid goalId, string detail, CancellationToken cancellationToken);

    /// <summary>Resplits a goal to fit the available execution capacity.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="reason">The reason for resplitting.</param>
    /// <param name="capacityLimit">The optional capacity limit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The replacement goals.</returns>
    Task<IReadOnlyList<Goal>> ResplitGoalAsync(Guid goalId, string reason, int? capacityLimit, CancellationToken cancellationToken);
}
