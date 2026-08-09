// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Represents persisted output from a completed goal.</summary>
public sealed class GoalResult
{
    /// <summary>Gets or sets the result identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the goal that produced the result.</summary>
    public Guid GoalId { get; set; }

    /// <summary>Gets or sets the goal that produced the result.</summary>
    public Goal Goal { get; set; } = null!;

    /// <summary>Gets or sets the executor output.</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>Gets or sets JSON metadata associated with the output.</summary>
    public string Metadata { get; set; } = "{}";

    /// <summary>Gets or sets when the result was completed.</summary>
    public DateTimeOffset CompletedAt { get; set; }
}
