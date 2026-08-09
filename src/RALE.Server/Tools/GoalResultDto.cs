// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Tools;

/// <summary>Represents the transport form of a persisted goal result.</summary>
/// <param name="Id">The result identifier.</param>
/// <param name="GoalId">The producing goal identifier.</param>
/// <param name="Output">The executor output.</param>
/// <param name="Metadata">JSON metadata associated with the output.</param>
/// <param name="CompletedAt">When the result completed.</param>
public sealed record GoalResultDto(Guid Id, Guid GoalId, string Output, string Metadata, DateTimeOffset CompletedAt);
