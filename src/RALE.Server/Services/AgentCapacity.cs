// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Services;

/// <summary>Represents a capacity observation for a registered agent.</summary>
public sealed record AgentCapacity
{
    /// <summary>Initializes a new instance of the <see cref="AgentCapacity"/> record.</summary>
    /// <param name="agentId">The registered agent identifier.</param>
    /// <param name="capacity">The available capacity.</param>
    /// <param name="maxConcurrentGoals">The maximum concurrent goal count.</param>
    /// <param name="constraintsJson">The serialized capacity constraints.</param>
    /// <param name="observedAt">When the capacity was observed.</param>
    /// <param name="expiresAt">When the capacity observation expires.</param>
    /// <param name="source">The capacity information source.</param>
    public AgentCapacity(
        Guid agentId,
        int capacity,
        int maxConcurrentGoals,
        string constraintsJson,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt,
        string source)
    {
        AgentId = agentId;
        Capacity = capacity;
        MaxConcurrentGoals = maxConcurrentGoals;
        ConstraintsJson = constraintsJson;
        ObservedAt = observedAt;
        ExpiresAt = expiresAt;
        Source = source;
    }

    /// <summary>Gets the registered agent identifier.</summary>
    public Guid AgentId { get; init; }

    /// <summary>Gets the available capacity.</summary>
    public int Capacity { get; init; }

    /// <summary>Gets the maximum concurrent goal count.</summary>
    public int MaxConcurrentGoals { get; init; }

    /// <summary>Gets the serialized capacity constraints.</summary>
    public string ConstraintsJson { get; init; }

    /// <summary>Gets when the capacity was observed.</summary>
    public DateTimeOffset ObservedAt { get; init; }

    /// <summary>Gets when the capacity observation expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Gets the capacity information source.</summary>
    public string Source { get; init; }
}
