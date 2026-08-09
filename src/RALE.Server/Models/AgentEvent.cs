// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Represents an auditable event associated with an execution agent.</summary>
public sealed class AgentEvent
{
    /// <summary>Gets or sets the event identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the affected agent.</summary>
    public Guid AgentId { get; set; }

    /// <summary>Gets or sets the affected agent.</summary>
    public Agent Agent { get; set; } = null!;

    /// <summary>Gets or sets the event type.</summary>
    public LoopEventType Type { get; set; }

    /// <summary>Gets or sets the event detail recorded in the audit trail.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets when the event was recorded.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
