// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Represents a registered execution agent and its current capacity profile.</summary>
public sealed class Agent
{
    /// <summary>Gets or sets the agent identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the human-readable agent name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-encoded capability list.</summary>
    public string Capabilities { get; set; } = "[]";

    /// <summary>Gets or sets the endpoint used for capacity discovery.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum number of goals the agent may execute concurrently.</summary>
    public int MaxConcurrentGoals { get; set; } = 1;

    /// <summary>Gets or sets the fallback maximum prompt or context capacity.</summary>
    public int MaxTokenCapacity { get; set; } = 4096;

    /// <summary>Gets or sets the JSON-encoded supported task-type list.</summary>
    public string SupportedTaskTypesJson { get; set; } = "[]";

    /// <summary>Gets or sets the service-level objective or SLA description.</summary>
    public string Sla { get; set; } = string.Empty;

    /// <summary>Gets or sets the reported security posture.</summary>
    public string SecurityPosture { get; set; } = "unverified";

    /// <summary>Gets or sets the trust level used by approval gates.</summary>
    public int TrustLevel { get; set; }

    /// <summary>Gets or sets the current number of assigned goals.</summary>
    public int CurrentLoad { get; set; }

    /// <summary>Gets or sets the JSON-encoded least-privilege tool-scope list.</summary>
    public string ToolScopesJson { get; set; } = "[]";

    /// <summary>Gets or sets the lifetime, in seconds, of cached capacity data.</summary>
    public int CapacityCacheTtlSeconds { get; set; } = 300;

    /// <summary>Gets or sets the last discovered capacity.</summary>
    public int? CachedCapacity { get; set; }

    /// <summary>Gets or sets the JSON-encoded constraints for cached capacity.</summary>
    public string CachedCapacityConstraintsJson { get; set; } = "{}";

    /// <summary>Gets or sets when capacity was last checked.</summary>
    public DateTimeOffset? CapacityCheckedAt { get; set; }

    /// <summary>Gets or sets when cached capacity expires.</summary>
    public DateTimeOffset? CapacityExpiresAt { get; set; }

    /// <summary>Gets or sets the identifier of the currently assigned goal.</summary>
    public Guid? AssignedGoalId { get; set; }

    /// <summary>Gets or sets the currently assigned goal.</summary>
    public Goal? AssignedGoal { get; set; }

    /// <summary>Gets or sets when the agent was registered.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the optimistic-concurrency version.</summary>
    public long Version { get; set; }

    /// <summary>Gets the audit events associated with the agent.</summary>
    public List<AgentEvent> Events { get; } = [];
}
