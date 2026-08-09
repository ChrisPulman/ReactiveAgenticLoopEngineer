// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Describes a goal emitted during prompt decomposition before persistence.</summary>
/// <param name="Sequence">The execution order assigned to the goal.</param>
/// <param name="Description">A concise description of the goal.</param>
/// <param name="Prompt">The executor prompt for the goal.</param>
public sealed record GoalDraft(int Sequence, string Description, string Prompt);
