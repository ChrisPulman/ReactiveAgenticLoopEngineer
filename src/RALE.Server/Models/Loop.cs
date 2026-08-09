// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Represents a persisted reactive execution loop.</summary>
public sealed class Loop
{
    /// <summary>Gets or sets the loop identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the primary objective to decompose.</summary>
    public string PrimaryObjective { get; set; } = string.Empty;

    /// <summary>Gets or sets when the loop was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the loop lifecycle status.</summary>
    public LoopStatus Status { get; set; } = LoopStatus.Pending;

    /// <summary>Gets or sets the maximum prompt length allowed for goals.</summary>
    public int TokenLimit { get; set; }

    /// <summary>Gets or sets JSON constraints applied to the loop.</summary>
    public string ConstraintsJson { get; set; } = "{}";

    /// <summary>Gets or sets the JSON-encoded required artifact list.</summary>
    public string RequiredArtifactsJson { get; set; } = "[]";

    /// <summary>Gets or sets the loop dispatch priority.</summary>
    public int Priority { get; set; }

    /// <summary>Gets or sets the optional loop deadline.</summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>Gets or sets the maximum number of reactive iterations.</summary>
    public int IterationLimit { get; set; } = 3;

    /// <summary>Gets or sets the requested execution pattern.</summary>
    public string ExecutionPattern { get; set; } = "serial";

    /// <summary>Gets or sets the optimistic-concurrency version.</summary>
    public long Version { get; set; }

    /// <summary>Gets the goals that belong to this loop.</summary>
    public List<Goal> Goals { get; } = [];

    /// <summary>Gets the audit events for this loop.</summary>
    public List<LoopEvent> Events { get; } = [];
}
