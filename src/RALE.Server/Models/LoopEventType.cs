// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Defines audit events emitted by loop and goal lifecycle operations.</summary>
public enum LoopEventType
{
    /// <summary>A loop was created.</summary>
    LoopCreated = 0,
    /// <summary>A goal was created.</summary>
    GoalCreated = 1,
    /// <summary>A goal was claimed by an executor.</summary>
    GoalClaimed = 2,
    /// <summary>A goal was paused.</summary>
    GoalPaused = 3,
    /// <summary>A goal was resumed.</summary>
    GoalResumed = 4,
    /// <summary>A goal completed successfully.</summary>
    GoalCompleted = 5,
    /// <summary>A goal failed.</summary>
    GoalFailed = 6,
    /// <summary>A loop was paused.</summary>
    LoopPaused = 7,
    /// <summary>A loop was resumed.</summary>
    LoopResumed = 8,
    /// <summary>A loop completed successfully.</summary>
    LoopCompleted = 9,
    /// <summary>An agent was registered.</summary>
    AgentRegistered = 10,
    /// <summary>Agent capacity was discovered.</summary>
    CapacityDiscovered = 11,
    /// <summary>A fallback capacity profile was used.</summary>
    CapacityFallbackUsed = 12,
    /// <summary>A plan was decomposed into goals.</summary>
    PlanDecomposed = 13,
    /// <summary>A goal was assigned to an agent.</summary>
    GoalAssigned = 14,
    /// <summary>A goal requires human approval.</summary>
    ApprovalRequired = 15,
    /// <summary>A goal was approved.</summary>
    GoalApproved = 16,
    /// <summary>A goal was rejected.</summary>
    GoalRejected = 17,
    /// <summary>An executor recorded progress for a goal.</summary>
    GoalHeartbeat = 18,
    /// <summary>A goal was split into replacement goals.</summary>
    GoalResplit = 19,
    /// <summary>A policy violation was recorded.</summary>
    PolicyViolation = 20
}
