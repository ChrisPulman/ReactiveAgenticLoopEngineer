// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Coordinates persisted reactive loops and their goal lifecycles.</summary>
public interface ILoopEngineer
{
    /// <summary>Creates a loop and decomposes its primary prompt into goals.</summary>
    /// <param name="primaryPrompt">The primary objective to decompose.</param>
    /// <param name="tokenLimit">The maximum prompt length allowed for each goal.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The newly created loop.</returns>
    Task<Loop> CreateLoop(string primaryPrompt, int tokenLimit, CancellationToken cancellationToken);

    /// <summary>Observes goals as they become ready for execution.</summary>
    /// <param name="loopId">The identifier of the loop to observe.</param>
    /// <returns>An observable sequence of ready goals.</returns>
    IObservable<Goal> ObserveNextGoals(Guid loopId);

    /// <summary>Decomposes the supplied loop state into its next goal.</summary>
    /// <param name="state">The state from which to create the next goal.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The newly persisted goal.</returns>
    Task<Goal> Decompose(LoopState state, CancellationToken cancellationToken);

    /// <summary>Persists a completed goal result and updates its loop.</summary>
    /// <param name="result">The result to persist.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous persistence operation.</returns>
    Task UpdateWithResult(GoalResult result, CancellationToken cancellationToken);

    /// <summary>Gets a loop by its identifier.</summary>
    /// <param name="loopId">The loop identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The loop, or <see langword="null"/> when it does not exist.</returns>
    Task<Loop?> GetLoopAsync(Guid loopId, CancellationToken cancellationToken);

    /// <summary>Lists goals in their loop execution order.</summary>
    /// <param name="loopId">The loop identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The goals belonging to the loop.</returns>
    Task<IReadOnlyList<Goal>> ListGoalsAsync(Guid loopId, CancellationToken cancellationToken);

    /// <summary>Claims the next ready goal in a loop.</summary>
    /// <param name="loopId">The loop identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The claimed goal, or <see langword="null"/> when none is ready.</returns>
    Task<Goal?> ClaimNextGoalAsync(Guid loopId, CancellationToken cancellationToken);

    /// <summary>Attempts to claim a goal using optimistic concurrency.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the goal was claimed; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryClaimGoalAsync(Guid goalId, CancellationToken cancellationToken);

    /// <summary>Pauses a goal.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated goal, or <see langword="null"/> when it does not exist.</returns>
    Task<Goal?> PauseGoalAsync(Guid goalId, CancellationToken cancellationToken);

    /// <summary>Resumes a paused goal.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated goal, or <see langword="null"/> when it does not exist.</returns>
    Task<Goal?> ResumeGoalAsync(Guid goalId, CancellationToken cancellationToken);

    /// <summary>Completes a goal and persists its output.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="output">The executor output.</param>
    /// <param name="metadata">Optional JSON metadata for the output.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The persisted result.</returns>
    Task<GoalResult> CompleteGoalAsync(Guid goalId, string output, string? metadata, CancellationToken cancellationToken);

    /// <summary>Marks a goal as failed and records the reason.</summary>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="reason">The failure reason.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated goal, or <see langword="null"/> when it does not exist.</returns>
    Task<Goal?> FailGoalAsync(Guid goalId, string reason, CancellationToken cancellationToken);
}
