// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Tools;

/// <summary>Represents the transport form of a registered agent.</summary>
/// <param name="Id">The agent identifier.</param>
/// <param name="Name">The human-readable agent name.</param>
/// <param name="Capabilities">The advertised capability list.</param>
/// <param name="Endpoint">The capacity-discovery endpoint.</param>
/// <param name="MaxConcurrentGoals">The maximum concurrent goal count.</param>
/// <param name="MaxTokenCapacity">The fallback prompt or context capacity.</param>
/// <param name="SupportedTaskTypes">The supported task types.</param>
/// <param name="Sla">The service-level objective or SLA description.</param>
/// <param name="SecurityPosture">The reported security posture.</param>
/// <param name="TrustLevel">The trust level used by approval gates.</param>
/// <param name="CurrentLoad">The current number of assigned goals.</param>
/// <param name="ToolScopes">The least-privilege tool scopes.</param>
/// <param name="CapacityCacheTtlSeconds">The capacity cache lifetime in seconds.</param>
/// <param name="CachedCapacity">The last discovered capacity.</param>
/// <param name="CachedCapacityConstraintsJson">JSON constraints for cached capacity.</param>
/// <param name="CapacityCheckedAt">When capacity was last checked.</param>
/// <param name="CapacityExpiresAt">When cached capacity expires.</param>
/// <param name="AssignedGoalId">The currently assigned goal identifier, if any.</param>
public sealed record AgentDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> Capabilities,
    string Endpoint,
    int MaxConcurrentGoals,
    int MaxTokenCapacity,
    IReadOnlyList<string> SupportedTaskTypes,
    string Sla,
    string SecurityPosture,
    int TrustLevel,
    int CurrentLoad,
    IReadOnlyList<string> ToolScopes,
    int CapacityCacheTtlSeconds,
    int? CachedCapacity,
    string CachedCapacityConstraintsJson,
    DateTimeOffset? CapacityCheckedAt,
    DateTimeOffset? CapacityExpiresAt,
    Guid? AssignedGoalId);
