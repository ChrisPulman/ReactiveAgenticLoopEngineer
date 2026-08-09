// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Defines the lifecycle states of a goal.</summary>
public enum GoalStatus
{
    /// <summary>The goal is awaiting dependency completion or dispatch.</summary>
    Pending = 0,
    /// <summary>The goal is currently being executed.</summary>
    InProgress = 1,
    /// <summary>The goal is paused and cannot be dispatched.</summary>
    Paused = 2,
    /// <summary>The goal completed successfully.</summary>
    Complete = 3,
    /// <summary>The goal completed unsuccessfully.</summary>
    Failed = 4,
    /// <summary>The goal was intentionally omitted from execution.</summary>
    Skipped = 5
}
