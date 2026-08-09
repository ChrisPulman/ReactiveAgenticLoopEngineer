// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Services;

/// <summary>Describes the registration profile of an execution agent.</summary>
public sealed record AgentCard
{
    /// <summary>Gets the human-readable agent name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the agent capabilities.</summary>
    public required IReadOnlyList<string> Capabilities { get; init; }

    /// <summary>Gets the capacity-discovery endpoint.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Gets the maximum concurrent goal count.</summary>
    public required int MaxConcurrentGoals { get; init; }

    /// <summary>Gets the supported token capacity.</summary>
    public required int MaxTokenCapacity { get; init; }

    /// <summary>Gets the supported task types.</summary>
    public required IReadOnlyList<string> SupportedTaskTypes { get; init; }

    /// <summary>Gets the service-level agreement description.</summary>
    public required string Sla { get; init; }

    /// <summary>Gets the reported security posture.</summary>
    public required string SecurityPosture { get; init; }

    /// <summary>Gets the trust level used for approval checks.</summary>
    public required int TrustLevel { get; init; }

    /// <summary>Gets the permitted tool scopes.</summary>
    public required IReadOnlyList<string> ToolScopes { get; init; }

    /// <summary>Gets the capacity-cache lifetime in seconds.</summary>
    public required int CapacityCacheTtlSeconds { get; init; }
}
