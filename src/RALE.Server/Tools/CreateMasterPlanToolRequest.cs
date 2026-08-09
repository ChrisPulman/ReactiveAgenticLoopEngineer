// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace RALE.Server.Tools;

/// <summary>Defines a governed master plan supplied to the RALE orchestrator.</summary>
public sealed class CreateMasterPlanToolRequest
{
    /// <summary>Gets the primary objective to decompose.</summary>
    [Description("Master plan or primary objective to decompose.")]
    public required string PrimaryObjective { get; init; }

    /// <summary>Gets the candidate agents that may receive tasks.</summary>
    [Description("Candidate agent ids for assignment.")]
    public required Guid[] AgentIds { get; init; }

    /// <summary>Gets the maximum character length for emitted task prompts.</summary>
    [Description("Maximum character length allowed for any emitted subtask prompt.")]
    public int TokenLimit { get; init; }

    /// <summary>Gets the optional task type used for capability matching.</summary>
    [Description("Task type used for capability and capacity matching.")]
    public string? TaskType { get; init; }

    /// <summary>Gets the requested serial or parallel execution pattern.</summary>
    [Description("Execution pattern: serial or parallel.")]
    public string? ExecutionPattern { get; init; }

    /// <summary>Gets the artifacts required from the plan.</summary>
    [Description("Required artifact names or categories.")]
    public required string[] RequiredArtifacts { get; init; }

    /// <summary>Gets the optional JSON constraints object.</summary>
    [Description("JSON constraints object for the plan.")]
    public string? ConstraintsJson { get; init; }

    /// <summary>Gets the plan dispatch priority.</summary>
    [Description("Plan priority. Higher values dispatch first.")]
    public int Priority { get; init; }

    /// <summary>Gets the optional deadline applied to generated tasks.</summary>
    [Description("Optional deadline for all subtasks.")]
    public DateTimeOffset? Deadline { get; init; }

    /// <summary>Gets a value indicating whether generated tasks require approval.</summary>
    [Description("Whether every generated task requires human approval before dispatch.")]
    public bool ApprovalRequired { get; init; }

    /// <summary>Gets the minimum agent trust level for unapproved dispatch.</summary>
    [Description("Minimum trust level before a task may dispatch without approval.")]
    public int MinTrustLevel { get; init; }

    /// <summary>Gets the tool scopes required by the plan.</summary>
    [Description("Tool scopes required by the plan.")]
    public required string[] ToolScopes { get; init; }

    /// <summary>Gets the maximum reactive completion-loop iteration count.</summary>
    [Description("Maximum reactive loop iterations before approval is required.")]
    public int IterationLimit { get; init; }

    /// <summary>Gets the retry limit applied to each task.</summary>
    [Description("Retry limit stored with each task.")]
    public int RetryLimit { get; init; }
}
