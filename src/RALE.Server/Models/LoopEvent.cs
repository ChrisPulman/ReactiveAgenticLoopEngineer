// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Represents an auditable event associated with a loop or goal.</summary>
public sealed class LoopEvent
{
    /// <summary>Gets or sets the event identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the affected loop.</summary>
    public Guid LoopId { get; set; }

    /// <summary>Gets or sets the affected loop.</summary>
    public Loop Loop { get; set; } = null!;

    /// <summary>Gets or sets the identifier of the affected goal, when applicable.</summary>
    public Guid? GoalId { get; set; }

    /// <summary>Gets or sets the affected goal, when applicable.</summary>
    public Goal? Goal { get; set; }

    /// <summary>Gets or sets the event type.</summary>
    public LoopEventType Type { get; set; }

    /// <summary>Gets or sets the event detail recorded in the audit trail.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets when the event was recorded.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
