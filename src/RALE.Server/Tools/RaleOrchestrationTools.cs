// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RALE.Server.Services;

namespace RALE.Server.Tools;

/// <summary>Provides MCP tools for registering agents and orchestrating RALE master plans.</summary>
[McpServerToolType]
public static class RaleOrchestrationTools
{
    /// <summary>Defines the default capacity-cache lifetime.</summary>
    private const int DefaultCapacityCacheTtlSeconds = 300;

    /// <summary>Defines the default maximum reactive iteration count.</summary>
    private const int DefaultIterationLimit = 3;

    /// <summary>Registers an agent card for orchestration.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="request">The agent card to register.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered agent.</returns>
    [McpServerTool(Name = "rale_register_agent", Title = "Register RALE Agent", Destructive = false, OpenWorld = false)]
    [Description("Registers an agent card with capabilities, capacity profile, supported task types, trust posture, and least-privilege tool scopes.")]
    public static async Task<AgentDto> RegisterAgent(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Agent card containing identity, capacity, task support, trust posture, and tool scopes.")] RegisterAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new McpException("name is required.");
        }

        var agent = await orchestrationEngineer.RegisterAgentAsync(
            new AgentCard
            {
                Name = request.Name,
                Capabilities = request.Capabilities ?? [],
                Endpoint = request.Endpoint ?? string.Empty,
                MaxConcurrentGoals = request.MaxConcurrentGoals,
                MaxTokenCapacity = request.MaxTokenCapacity,
                SupportedTaskTypes = request.SupportedTaskTypes ?? [],
                Sla = request.Sla ?? string.Empty,
                SecurityPosture = request.SecurityPosture ?? "unverified",
                TrustLevel = request.TrustLevel,
                ToolScopes = request.ToolScopes ?? [],
                CapacityCacheTtlSeconds = request.CapacityCacheTtlSeconds <= 0 ? DefaultCapacityCacheTtlSeconds : request.CapacityCacheTtlSeconds,
            },
            cancellationToken).ConfigureAwait(false);

        return agent.ToDto();
    }

    /// <summary>Lists registered agent cards.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered agents.</returns>
    [McpServerTool(Name = "rale_list_agents", Title = "List RALE Agents", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Lists registered agent cards with load, capacity cache, trust, and tool-scope metadata.")]
    public static async Task<IReadOnlyList<AgentDto>> ListAgents(
        IOrchestrationEngineer orchestrationEngineer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        var agents = await orchestrationEngineer.ListAgentsAsync(cancellationToken).ConfigureAwait(false);
        var agentDtos = new AgentDto[agents.Count];
        for (var index = 0; index < agents.Count; index++)
        {
            agentDtos[index] = agents[index].ToDto();
        }

        return agentDtos;
    }

    /// <summary>Discovers task-specific capacity for an agent.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="taskProfile">The task profile used for discovery.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The discovered capacity observation.</returns>
    [McpServerTool(Name = "rale_discover_agent_capacity", Title = "Discover RALE Agent Capacity", Destructive = false, OpenWorld = true)]
    [Description("Queries an agent endpoint for task-specific capacity, falling back to a fresh cached profile or the registered profile when needed.")]
    public static async Task<AgentCapacityDto> DiscoverAgentCapacity(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Agent id whose capacity should be discovered.")] Guid agentId,
        [Description("Task profile or task type used for the capacity query.")] string taskProfile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        var capacity = await orchestrationEngineer.DiscoverCapacityAsync(agentId, taskProfile ?? string.Empty, cancellationToken).ConfigureAwait(false);
        return capacity.ToDto();
    }

    /// <summary>Creates a persisted master plan and assigns capacity-fit subtasks.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="request">The objective, candidate agents, execution policy, and governance metadata.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created master-plan loop.</returns>
    [McpServerTool(Name = "rale_create_master_plan", Title = "Create RALE Master Plan", Destructive = false, OpenWorld = false)]
    [Description("Creates a persisted master-plan loop, discovers agent capacities, splits the plan into capacity-fit subtasks, and records dependency and governance metadata.")]
    public static async Task<LoopDto> CreateMasterPlan(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Master-plan objective, agent candidates, execution policy, constraints, and governance metadata.")] CreateMasterPlanToolRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PrimaryObjective))
        {
            throw new McpException("primaryObjective is required.");
        }

        if (request.AgentIds is null || request.AgentIds.Length == 0)
        {
            throw new McpException("agentIds must contain at least one agent id.");
        }

        var loop = await orchestrationEngineer.CreateMasterPlanAsync(
            CreateMasterPlanRequest(request),
            cancellationToken).ConfigureAwait(false);

        return loop.ToDto();
    }

    /// <summary>Assigns the next ready task to a specific agent.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="loopId">The task graph's loop identifier.</param>
    /// <param name="agentId">The requesting agent identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The assigned goal, or <see langword="null"/> when none is ready.</returns>
    [McpServerTool(Name = "rale_assign_next_task", Title = "Assign Next RALE Task", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Assigns the next ready task for a specific agent, enforcing dependencies, load, policy, and approval gates.")]
    public static async Task<GoalDto?> AssignNextTask(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Loop id whose task graph should be dispatched.")] Guid loopId,
        [Description("Agent id requesting the next task.")] Guid agentId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        var goal = await orchestrationEngineer.AssignNextGoalAsync(loopId, agentId, cancellationToken).ConfigureAwait(false);
        return goal?.ToDto();
    }

    /// <summary>Approves or rejects a goal blocked by a human approval gate.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="approved">Whether to approve the goal.</param>
    /// <param name="reviewer">The reviewer name recorded in the audit trail.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated goal, or <see langword="null"/> when it does not exist.</returns>
    [McpServerTool(Name = "rale_approve_goal", Title = "Approve RALE Goal", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Approves or rejects a goal that is blocked by a human approval gate.")]
    public static async Task<GoalDto?> ApproveGoal(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Goal id to approve or reject.")] Guid goalId,
        [Description("True to approve; false to reject.")] bool approved,
        [Description("Reviewer or approver name recorded in the audit trail.")] string? reviewer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        var goal = await orchestrationEngineer.ApproveGoalAsync(goalId, approved, reviewer ?? "unknown reviewer", cancellationToken).ConfigureAwait(false);
        return goal?.ToDto();
    }

    /// <summary>Records executor progress for a goal.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="detail">Optional execution detail to record.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated goal, or <see langword="null"/> when it does not exist.</returns>
    [McpServerTool(Name = "rale_record_goal_heartbeat", Title = "Record RALE Goal Heartbeat", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Records an execution heartbeat for a goal so long-running agent loops remain observable.")]
    public static async Task<GoalDto?> RecordGoalHeartbeat(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Goal id receiving a heartbeat.")] Guid goalId,
        [Description("Execution detail, status, or provenance note.")] string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        var goal = await orchestrationEngineer.RecordHeartbeatAsync(goalId, detail ?? "Agent heartbeat.", cancellationToken).ConfigureAwait(false);
        return goal?.ToDto();
    }

    /// <summary>Splits a goal into smaller replacement tasks.</summary>
    /// <param name="orchestrationEngineer">The orchestration service.</param>
    /// <param name="goalId">The goal identifier.</param>
    /// <param name="reason">The audit reason for the split.</param>
    /// <param name="capacityLimit">An optional replacement prompt limit.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The replacement goals.</returns>
    [McpServerTool(Name = "rale_resplit_goal", Title = "Re-split RALE Goal", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Re-splits a goal into smaller replacement tasks after capacity mismatch or bounded loop failure, preserving dependency handoff.")]
    public static async Task<IReadOnlyList<GoalDto>> ResplitGoal(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Goal id to re-split.")] Guid goalId,
        [Description("Reason recorded in the audit trail.")] string? reason,
        [Description("Optional replacement prompt limit. Uses assigned agent capacity when omitted.")] int? capacityLimit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        var goals = await orchestrationEngineer.ResplitGoalAsync(goalId, reason ?? "capacity mismatch", capacityLimit, cancellationToken).ConfigureAwait(false);
        var goalDtos = new GoalDto[goals.Count];
        for (var index = 0; index < goals.Count; index++)
        {
            goalDtos[index] = goals[index].ToDto();
        }

        return goalDtos;
    }

    /// <summary>Maps a tool request to the required master-plan request.</summary>
    /// <param name="request">The tool request to map.</param>
    /// <returns>The normalized master-plan request.</returns>
    private static MasterPlanRequest CreateMasterPlanRequest(CreateMasterPlanToolRequest request) => new()
    {
        PrimaryObjective = request.PrimaryObjective,
        AgentIds = request.AgentIds,
        TokenLimit = request.TokenLimit,
        TaskType = string.IsNullOrWhiteSpace(request.TaskType) ? "general" : request.TaskType.Trim(),
        ExecutionPattern = string.IsNullOrWhiteSpace(request.ExecutionPattern) ? "serial" : request.ExecutionPattern.Trim(),
        RequiredArtifacts = request.RequiredArtifacts ?? [],
        ConstraintsJson = string.IsNullOrWhiteSpace(request.ConstraintsJson) ? "{}" : request.ConstraintsJson.Trim(),
        Priority = request.Priority,
        Deadline = request.Deadline,
        ApprovalRequired = request.ApprovalRequired,
        MinTrustLevel = request.MinTrustLevel,
        ToolScopes = request.ToolScopes ?? [],
        IterationLimit = request.IterationLimit <= 0 ? DefaultIterationLimit : request.IterationLimit,
        RetryLimit = request.RetryLimit < 0 ? 0 : request.RetryLimit,
    };
}
