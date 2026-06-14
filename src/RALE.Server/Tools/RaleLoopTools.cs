using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RALE.Server.Services;

namespace RALE.Server.Tools;

[McpServerToolType]
public static class RaleLoopTools
{
    [McpServerTool(Name = "rale_create_loop", Title = "Create RALE Loop", Destructive = false, OpenWorld = false)]
    [Description("Creates a persisted RALE loop and decomposes the primary prompt into ordered goals that respect the configured prompt limit.")]
    public static async Task<LoopDto> CreateLoop(
        ILoopEngineer loopEngineer,
        [Description("Primary objective to decompose into goal prompts.")] string primaryPrompt,
        [Description("Maximum character length allowed for each emitted goal prompt.")] int tokenLimit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loopEngineer);

        if (string.IsNullOrWhiteSpace(primaryPrompt))
        {
            throw new McpException("primaryPrompt is required.");
        }

        if (tokenLimit <= 0)
        {
            throw new McpException("tokenLimit must be greater than zero.");
        }

        var loop = await loopEngineer.CreateLoop(primaryPrompt, tokenLimit, cancellationToken).ConfigureAwait(false);
        return loop.ToDto();
    }

    [McpServerTool(Name = "rale_get_loop", Title = "Get RALE Loop", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Gets a persisted loop with its ordered goal list.")]
    public static async Task<LoopDto> GetLoop(
        ILoopEngineer loopEngineer,
        [Description("Loop id to fetch.")] Guid loopId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loopEngineer);

        var loop = await loopEngineer.GetLoopAsync(loopId, cancellationToken).ConfigureAwait(false)
            ?? throw new McpException($"Loop '{loopId}' was not found.");

        return loop.ToDto();
    }

    [McpServerTool(Name = "rale_list_goals", Title = "List RALE Goals", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Lists all goals for a loop in execution order.")]
    public static async Task<IReadOnlyList<GoalDto>> ListGoals(
        ILoopEngineer loopEngineer,
        [Description("Loop id whose goals should be listed.")] Guid loopId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loopEngineer);

        var goals = await loopEngineer.ListGoalsAsync(loopId, cancellationToken).ConfigureAwait(false);
        return goals.Select(goal => goal.ToDto()).ToArray();
    }

    [McpServerTool(Name = "rale_claim_next_goal", Title = "Claim Next RALE Goal", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Claims the next ready pending goal using optimistic concurrency so only one executor can own it.")]
    public static async Task<GoalDto?> ClaimNextGoal(
        ILoopEngineer loopEngineer,
        [Description("Loop id to claim a ready goal from.")] Guid loopId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loopEngineer);

        var goal = await loopEngineer.ClaimNextGoalAsync(loopId, cancellationToken).ConfigureAwait(false);
        return goal?.ToDto();
    }

    [McpServerTool(Name = "rale_complete_goal", Title = "Complete RALE Goal", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Persists a goal result, marks the goal complete, and emits dependent goals when they become ready.")]
    public static async Task<GoalResultDto> CompleteGoal(
        ILoopEngineer loopEngineer,
        [Description("Goal id to complete.")] Guid goalId,
        [Description("Agent output to persist for the goal.")] string output,
        [Description("Optional JSON metadata for the result.")] string? metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loopEngineer);

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new McpException("output is required.");
        }

        var result = await loopEngineer.CompleteGoalAsync(goalId, output, metadata, cancellationToken).ConfigureAwait(false);
        return result.ToDto();
    }

    [McpServerTool(Name = "rale_pause_goal", Title = "Pause RALE Goal", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Pauses a pending or in-progress goal so it will not be emitted until resumed.")]
    public static async Task<GoalDto?> PauseGoal(
        ILoopEngineer loopEngineer,
        [Description("Goal id to pause.")] Guid goalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loopEngineer);

        var goal = await loopEngineer.PauseGoalAsync(goalId, cancellationToken).ConfigureAwait(false);
        return goal?.ToDto();
    }

    [McpServerTool(Name = "rale_resume_goal", Title = "Resume RALE Goal", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Resumes a paused goal and emits it if its dependencies are complete.")]
    public static async Task<GoalDto?> ResumeGoal(
        ILoopEngineer loopEngineer,
        [Description("Goal id to resume.")] Guid goalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loopEngineer);

        var goal = await loopEngineer.ResumeGoalAsync(goalId, cancellationToken).ConfigureAwait(false);
        return goal?.ToDto();
    }
}
