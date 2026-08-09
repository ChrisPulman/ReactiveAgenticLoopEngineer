// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Services;

/// <summary>Describes the input required to create a persisted master plan.</summary>
public sealed record MasterPlanRequest
{
    /// <summary>Gets the objective to achieve.</summary>
    public required string PrimaryObjective { get; init; }

    /// <summary>Gets the eligible agents.</summary>
    public required IReadOnlyList<Guid> AgentIds { get; init; }

    /// <summary>Gets the maximum token budget.</summary>
    public required int TokenLimit { get; init; }

    /// <summary>Gets the required task type.</summary>
    public required string TaskType { get; init; }

    /// <summary>Gets the requested execution pattern.</summary>
    public required string ExecutionPattern { get; init; }

    /// <summary>Gets the required output artifacts.</summary>
    public required IReadOnlyList<string> RequiredArtifacts { get; init; }

    /// <summary>Gets the serialized execution constraints.</summary>
    public required string ConstraintsJson { get; init; }

    /// <summary>Gets the dispatch priority.</summary>
    public required int Priority { get; init; }

    /// <summary>Gets the optional completion deadline.</summary>
    public DateTimeOffset? Deadline { get; init; }

    /// <summary>Gets whether approval is required before dispatch.</summary>
    public required bool ApprovalRequired { get; init; }

    /// <summary>Gets the minimum agent trust level.</summary>
    public required int MinTrustLevel { get; init; }

    /// <summary>Gets the permitted tool scopes.</summary>
    public required IReadOnlyList<string> ToolScopes { get; init; }

    /// <summary>Gets the maximum planning iterations.</summary>
    public required int IterationLimit { get; init; }

    /// <summary>Gets the maximum retries for a goal.</summary>
    public required int RetryLimit { get; init; }
}
