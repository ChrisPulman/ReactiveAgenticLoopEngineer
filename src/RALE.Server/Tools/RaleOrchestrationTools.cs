using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RALE.Server.Services;

namespace RALE.Server.Tools;

[McpServerToolType]
public static class RaleOrchestrationTools
{
    [McpServerTool(Name = "rale_register_agent", Title = "Register RALE Agent", Destructive = false, OpenWorld = false)]
    [Description("Registers an agent card with capabilities, capacity profile, supported task types, trust posture, and least-privilege tool scopes.")]
    public static async Task<AgentDto> RegisterAgent(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Human-readable unique agent name.")] string name,
        [Description("Agent capabilities such as csharp, testing, docs, or mcp.")] string[] capabilities,
        [Description("Agent HTTP endpoint used for on-demand capacity discovery.")] string? endpoint,
        [Description("Maximum goals this agent may execute concurrently.")] int maxConcurrentGoals,
        [Description("Fallback max prompt/context capacity for the agent.")] int maxTokenCapacity,
        [Description("Task types the agent supports.")] string[] supportedTaskTypes,
        [Description("Service-level objective or SLA description.")] string? sla,
        [Description("Security posture such as unverified, verified, or trusted.")] string? securityPosture,
        [Description("Trust level from 0 to 100 used by approval gates.")] int trustLevel,
        [Description("Least-privilege tool scopes the agent may use.")] string[] toolScopes,
        [Description("Seconds before cached capacity expires.")] int capacityCacheTtlSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new McpException("name is required.");
        }

        var agent = await orchestrationEngineer.RegisterAgentAsync(
            new AgentCard(
                name,
                capabilities ?? [],
                endpoint ?? string.Empty,
                maxConcurrentGoals,
                maxTokenCapacity,
                supportedTaskTypes ?? [],
                sla ?? string.Empty,
                securityPosture ?? "unverified",
                trustLevel,
                toolScopes ?? [],
                capacityCacheTtlSeconds <= 0 ? 300 : capacityCacheTtlSeconds),
            cancellationToken).ConfigureAwait(false);

        return agent.ToDto();
    }

    [McpServerTool(Name = "rale_list_agents", Title = "List RALE Agents", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Lists registered agent cards with load, capacity cache, trust, and tool-scope metadata.")]
    public static async Task<IReadOnlyList<AgentDto>> ListAgents(
        IOrchestrationEngineer orchestrationEngineer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        var agents = await orchestrationEngineer.ListAgentsAsync(cancellationToken).ConfigureAwait(false);
        return [.. agents.Select(agent => agent.ToDto())];
    }

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

    [McpServerTool(Name = "rale_create_master_plan", Title = "Create RALE Master Plan", Destructive = false, OpenWorld = false)]
    [Description("Creates a persisted master-plan loop, discovers agent capacities, splits the plan into capacity-fit subtasks, and records dependency and governance metadata.")]
    public static async Task<LoopDto> CreateMasterPlan(
        IOrchestrationEngineer orchestrationEngineer,
        [Description("Master plan or primary objective to decompose.")] string primaryObjective,
        [Description("Candidate agent ids for assignment.")] Guid[] agentIds,
        [Description("Maximum character length allowed for any emitted subtask prompt.")] int tokenLimit,
        [Description("Task type used for capability and capacity matching.")] string? taskType,
        [Description("Execution pattern: serial or parallel.")] string? executionPattern,
        [Description("Required artifact names or categories.")] string[] requiredArtifacts,
        [Description("JSON constraints object for the plan.")] string? constraintsJson,
        [Description("Plan priority. Higher values dispatch first.")] int priority,
        [Description("Optional deadline for all subtasks.")] DateTimeOffset? deadline,
        [Description("Whether every generated task requires human approval before dispatch.")] bool approvalRequired,
        [Description("Minimum trust level before a task may dispatch without approval.")] int minTrustLevel,
        [Description("Tool scopes required by the plan.")] string[] toolScopes,
        [Description("Maximum reactive loop iterations before approval is required.")] int iterationLimit,
        [Description("Retry limit stored with each task.")] int retryLimit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrationEngineer);

        if (string.IsNullOrWhiteSpace(primaryObjective))
        {
            throw new McpException("primaryObjective is required.");
        }

        if (agentIds is null || agentIds.Length == 0)
        {
            throw new McpException("agentIds must contain at least one agent id.");
        }

        var loop = await orchestrationEngineer.CreateMasterPlanAsync(
            new MasterPlanRequest(
                primaryObjective,
                agentIds,
                tokenLimit,
                string.IsNullOrWhiteSpace(taskType) ? "general" : taskType.Trim(),
                string.IsNullOrWhiteSpace(executionPattern) ? "serial" : executionPattern.Trim(),
                requiredArtifacts ?? [],
                string.IsNullOrWhiteSpace(constraintsJson) ? "{}" : constraintsJson.Trim(),
                priority,
                deadline,
                approvalRequired,
                minTrustLevel,
                toolScopes ?? [],
                iterationLimit <= 0 ? 3 : iterationLimit,
                retryLimit < 0 ? 0 : retryLimit),
            cancellationToken).ConfigureAwait(false);

        return loop.ToDto();
    }

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
        return [.. goals.Select(goal => goal.ToDto())];
    }
}
