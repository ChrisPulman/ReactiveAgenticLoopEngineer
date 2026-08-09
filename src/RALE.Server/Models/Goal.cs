// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Represents a persisted, independently executable unit of work in a loop.</summary>
public sealed class Goal
{
    /// <summary>Gets or sets the goal identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the containing loop.</summary>
    public Guid LoopId { get; set; }

    /// <summary>Gets or sets the containing loop.</summary>
    public Loop Loop { get; set; } = null!;

    /// <summary>Gets or sets the execution order within the loop.</summary>
    public int Sequence { get; set; }

    /// <summary>Gets or sets the concise goal description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the prompt supplied to the executor.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-encoded dependency identifier list.</summary>
    public string DependsOnJson { get; set; } = "[]";

    /// <summary>Gets or sets the identifier of the assigned agent.</summary>
    public Guid? AssignedAgentId { get; set; }

    /// <summary>Gets or sets the assigned agent.</summary>
    public Agent? AssignedAgent { get; set; }

    /// <summary>Gets or sets the task type used for capability matching.</summary>
    public string TaskType { get; set; } = "general";

    /// <summary>Gets or sets the dispatch priority.</summary>
    public int Priority { get; set; }

    /// <summary>Gets or sets the optional completion deadline.</summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>Gets or sets the JSON-encoded required artifact list.</summary>
    public string RequiredArtifactsJson { get; set; } = "[]";

    /// <summary>Gets or sets whether human approval is required before dispatch.</summary>
    public bool ApprovalRequired { get; set; }

    /// <summary>Gets or sets the approval-gate state.</summary>
    public string ApprovalState { get; set; } = "NotRequired";

    /// <summary>Gets or sets the maximum permitted reactive iterations.</summary>
    public int IterationLimit { get; set; } = 3;

    /// <summary>Gets or sets the completed reactive iteration count.</summary>
    public int IterationCount { get; set; }

    /// <summary>Gets or sets the maximum number of retries.</summary>
    public int RetryLimit { get; set; } = 2;

    /// <summary>Gets or sets the number of retries already used.</summary>
    public int RetryCount { get; set; }

    /// <summary>Gets or sets the policy evaluation state.</summary>
    public string PolicyState { get; set; } = "Allowed";

    /// <summary>Gets or sets the JSON-encoded policy violation list.</summary>
    public string PolicyViolationsJson { get; set; } = "[]";

    /// <summary>Gets or sets when the executor most recently reported progress.</summary>
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    /// <summary>Gets or sets the lifecycle status.</summary>
    public GoalStatus Status { get; set; } = GoalStatus.Pending;

    /// <summary>Gets or sets when execution began.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets when execution completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the optimistic-concurrency version.</summary>
    public long Version { get; set; }

    /// <summary>Gets the persisted results for this goal.</summary>
    public List<GoalResult> Results { get; } = [];
}
