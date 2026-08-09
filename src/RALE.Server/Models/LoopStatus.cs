// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Models;

/// <summary>Defines the lifecycle states of a loop.</summary>
public enum LoopStatus
{
    /// <summary>The loop is awaiting execution.</summary>
    Pending = 0,
    /// <summary>The loop is actively executing goals.</summary>
    Running = 1,
    /// <summary>The loop is paused.</summary>
    Paused = 2,
    /// <summary>The loop completed successfully.</summary>
    Complete = 3,
    /// <summary>The loop completed unsuccessfully.</summary>
    Failed = 4
}
