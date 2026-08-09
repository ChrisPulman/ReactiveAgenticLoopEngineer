// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Captures the state used to decompose the next goal in a loop.</summary>
/// <param name="LoopId">The identifier of the loop being decomposed.</param>
/// <param name="PrimaryPrompt">The primary prompt or objective.</param>
/// <param name="TokenLimit">The maximum prompt length permitted for a goal.</param>
/// <param name="ExistingGoalCount">The number of goals already persisted.</param>
/// <param name="CompletedOutputs">Outputs produced by completed goals.</param>
public sealed record LoopState(
    Guid LoopId,
    string PrimaryPrompt,
    int TokenLimit,
    int ExistingGoalCount,
    IReadOnlyList<string> CompletedOutputs);
