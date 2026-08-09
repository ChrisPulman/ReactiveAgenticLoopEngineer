// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Tools;

/// <summary>Represents a discovered agent capacity observation.</summary>
/// <param name="AgentId">The agent identifier.</param>
/// <param name="Capacity">The available task capacity.</param>
/// <param name="MaxConcurrentGoals">The registered concurrent-goal limit.</param>
/// <param name="ConstraintsJson">JSON constraints associated with the observation.</param>
/// <param name="ObservedAt">When the capacity was observed.</param>
/// <param name="ExpiresAt">When the observation expires.</param>
/// <param name="Source">The source of the capacity value.</param>
public sealed record AgentCapacityDto(
    Guid AgentId,
    int Capacity,
    int MaxConcurrentGoals,
    string ConstraintsJson,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string Source);
