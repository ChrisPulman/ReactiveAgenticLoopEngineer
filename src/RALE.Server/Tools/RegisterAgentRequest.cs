// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace RALE.Server.Tools;

/// <summary>Defines an agent card supplied to the RALE orchestrator.</summary>
public sealed class RegisterAgentRequest
{
    /// <summary>Gets the human-readable unique agent name.</summary>
    [Description("Human-readable unique agent name.")]
    public required string Name { get; init; }

    /// <summary>Gets the capabilities advertised by the agent.</summary>
    [Description("Agent capabilities such as csharp, testing, docs, or mcp.")]
    public required string[] Capabilities { get; init; }

    /// <summary>Gets the optional HTTP endpoint used for capacity discovery.</summary>
    [Description("Agent HTTP endpoint used for on-demand capacity discovery.")]
    public string? Endpoint { get; init; }

    /// <summary>Gets the maximum number of goals the agent can execute concurrently.</summary>
    [Description("Maximum goals this agent may execute concurrently.")]
    public int MaxConcurrentGoals { get; init; }

    /// <summary>Gets the fallback prompt or context capacity.</summary>
    [Description("Fallback max prompt/context capacity for the agent.")]
    public int MaxTokenCapacity { get; init; }

    /// <summary>Gets the task types supported by the agent.</summary>
    [Description("Task types the agent supports.")]
    public required string[] SupportedTaskTypes { get; init; }

    /// <summary>Gets the optional service-level objective.</summary>
    [Description("Service-level objective or SLA description.")]
    public string? Sla { get; init; }

    /// <summary>Gets the optional reported security posture.</summary>
    [Description("Security posture such as unverified, verified, or trusted.")]
    public string? SecurityPosture { get; init; }

    /// <summary>Gets the trust level used by approval gates.</summary>
    [Description("Trust level from 0 to 100 used by approval gates.")]
    public int TrustLevel { get; init; }

    /// <summary>Gets the least-privilege tool scopes available to the agent.</summary>
    [Description("Least-privilege tool scopes the agent may use.")]
    public required string[] ToolScopes { get; init; }

    /// <summary>Gets the lifetime of a cached capacity observation in seconds.</summary>
    [Description("Seconds before cached capacity expires.")]
    public int CapacityCacheTtlSeconds { get; init; }
}
