// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Tools;

/// <summary>Represents the transport form of a persisted goal.</summary>
/// <param name="Id">The goal identifier.</param>
/// <param name="LoopId">The containing loop identifier.</param>
/// <param name="Sequence">The execution order.</param>
/// <param name="Description">A concise goal description.</param>
/// <param name="Prompt">The executor prompt.</param>
/// <param name="DependsOn">The prerequisite goal identifiers.</param>
/// <param name="AssignedAgentId">The assigned agent identifier, if any.</param>
/// <param name="TaskType">The task type used for capability matching.</param>
/// <param name="Priority">The dispatch priority.</param>
/// <param name="Deadline">The optional completion deadline.</param>
/// <param name="RequiredArtifacts">Artifacts required from the goal.</param>
/// <param name="ApprovalRequired">Whether human approval is required before dispatch.</param>
/// <param name="ApprovalState">The approval-gate state.</param>
/// <param name="IterationLimit">The maximum reactive iteration count.</param>
/// <param name="IterationCount">The completed reactive iteration count.</param>
/// <param name="RetryLimit">The maximum retry count.</param>
/// <param name="RetryCount">The retries already used.</param>
/// <param name="PolicyState">The policy evaluation state.</param>
/// <param name="PolicyViolations">Policy violations recorded for the goal.</param>
/// <param name="Status">The goal lifecycle status.</param>
/// <param name="StartedAt">When execution began.</param>
/// <param name="CompletedAt">When execution completed.</param>
/// <param name="LastHeartbeatAt">When the executor last reported progress.</param>
public sealed record GoalDto(
    Guid Id,
    Guid LoopId,
    int Sequence,
    string Description,
    string Prompt,
    IReadOnlyList<Guid> DependsOn,
    Guid? AssignedAgentId,
    string TaskType,
    int Priority,
    DateTimeOffset? Deadline,
    IReadOnlyList<string> RequiredArtifacts,
    bool ApprovalRequired,
    string ApprovalState,
    int IterationLimit,
    int IterationCount,
    int RetryLimit,
    int RetryCount,
    string PolicyState,
    IReadOnlyList<string> PolicyViolations,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LastHeartbeatAt);
