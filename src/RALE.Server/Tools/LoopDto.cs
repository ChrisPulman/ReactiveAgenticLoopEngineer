// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Tools;

/// <summary>Represents the transport form of a persisted loop.</summary>
/// <param name="Id">The loop identifier.</param>
/// <param name="PrimaryObjective">The loop's primary objective.</param>
/// <param name="CreatedAt">When the loop was created.</param>
/// <param name="Status">The loop lifecycle status.</param>
/// <param name="TokenLimit">The maximum prompt length allowed for goals.</param>
/// <param name="ExecutionPattern">The requested execution pattern.</param>
/// <param name="ConstraintsJson">JSON constraints applied to the loop.</param>
/// <param name="RequiredArtifacts">Artifacts required from the loop.</param>
/// <param name="Priority">The dispatch priority.</param>
/// <param name="Deadline">The optional completion deadline.</param>
/// <param name="Goals">The loop's ordered goals.</param>
public sealed record LoopDto(
    Guid Id,
    string PrimaryObjective,
    DateTimeOffset CreatedAt,
    string Status,
    int TokenLimit,
    string ExecutionPattern,
    string ConstraintsJson,
    IReadOnlyList<string> RequiredArtifacts,
    int Priority,
    DateTimeOffset? Deadline,
    IReadOnlyList<GoalDto> Goals);
