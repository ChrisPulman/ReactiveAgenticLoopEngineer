using System.Text.Json;
using RALE.Server.Models;

namespace RALE.Server.Tools;

public sealed record LoopDto(
    Guid Id,
    string PrimaryObjective,
    DateTimeOffset CreatedAt,
    string Status,
    int TokenLimit,
    string ExecutionPattern,
    string ConstraintsJson,
    IReadOnlyList<string> RequiredArtifacts,
    int Priority,
    DateTimeOffset? Deadline,
    IReadOnlyList<GoalDto> Goals);

public sealed record GoalDto(
    Guid Id,
    Guid LoopId,
    int Sequence,
    string Description,
    string Prompt,
    IReadOnlyList<Guid> DependsOn,
    Guid? AssignedAgentId,
    string TaskType,
    int Priority,
    DateTimeOffset? Deadline,
    IReadOnlyList<string> RequiredArtifacts,
    bool ApprovalRequired,
    string ApprovalState,
    int IterationLimit,
    int IterationCount,
    int RetryLimit,
    int RetryCount,
    string PolicyState,
    IReadOnlyList<string> PolicyViolations,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LastHeartbeatAt);

public sealed record GoalResultDto(Guid Id, Guid GoalId, string Output, string Metadata, DateTimeOffset CompletedAt);

public sealed record AgentDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> Capabilities,
    string Endpoint,
    int MaxConcurrentGoals,
    int MaxTokenCapacity,
    IReadOnlyList<string> SupportedTaskTypes,
    string Sla,
    string SecurityPosture,
    int TrustLevel,
    int CurrentLoad,
    IReadOnlyList<string> ToolScopes,
    int CapacityCacheTtlSeconds,
    int? CachedCapacity,
    string CachedCapacityConstraintsJson,
    DateTimeOffset? CapacityCheckedAt,
    DateTimeOffset? CapacityExpiresAt,
    Guid? AssignedGoalId);

public sealed record AgentCapacityDto(
    Guid AgentId,
    int Capacity,
    int MaxConcurrentGoals,
    string ConstraintsJson,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string Source);

public static class RaleDtoMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LoopDto ToDto(this Loop loop) =>
        loop is null
            ? throw new ArgumentNullException(nameof(loop))
            : new(
            loop.Id,
            loop.PrimaryObjective,
            loop.CreatedAt,
            loop.Status.ToString(),
            loop.TokenLimit,
            loop.ExecutionPattern,
            loop.ConstraintsJson,
            ParseStrings(loop.RequiredArtifactsJson),
            loop.Priority,
            loop.Deadline,
            loop.Goals.OrderBy(goal => goal.Sequence).Select(ToDto).ToArray());

    public static GoalDto ToDto(this Goal goal) =>
        goal is null
            ? throw new ArgumentNullException(nameof(goal))
            : new(
            goal.Id,
            goal.LoopId,
            goal.Sequence,
            goal.Description,
            goal.Prompt,
            ParseDependencies(goal.DependsOnJson),
            goal.AssignedAgentId,
            goal.TaskType,
            goal.Priority,
            goal.Deadline,
            ParseStrings(goal.RequiredArtifactsJson),
            goal.ApprovalRequired,
            goal.ApprovalState,
            goal.IterationLimit,
            goal.IterationCount,
            goal.RetryLimit,
            goal.RetryCount,
            goal.PolicyState,
            ParseStrings(goal.PolicyViolationsJson),
            goal.Status.ToString(),
            goal.StartedAt,
            goal.CompletedAt,
            goal.LastHeartbeatAt);

    public static GoalResultDto ToDto(this GoalResult result) =>
        result is null
            ? throw new ArgumentNullException(nameof(result))
            : new(result.Id, result.GoalId, result.Output, result.Metadata, result.CompletedAt);

    public static AgentDto ToDto(this Agent agent) =>
        agent is null
            ? throw new ArgumentNullException(nameof(agent))
            : new(
                agent.Id,
                agent.Name,
                ParseStrings(agent.Capabilities),
                agent.Endpoint,
                agent.MaxConcurrentGoals,
                agent.MaxTokenCapacity,
                ParseStrings(agent.SupportedTaskTypesJson),
                agent.Sla,
                agent.SecurityPosture,
                agent.TrustLevel,
                agent.CurrentLoad,
                ParseStrings(agent.ToolScopesJson),
                agent.CapacityCacheTtlSeconds,
                agent.CachedCapacity,
                agent.CachedCapacityConstraintsJson,
                agent.CapacityCheckedAt,
                agent.CapacityExpiresAt,
                agent.AssignedGoalId);

    public static AgentCapacityDto ToDto(this Services.AgentCapacity capacity) =>
        capacity is null
            ? throw new ArgumentNullException(nameof(capacity))
            : new(
                capacity.AgentId,
                capacity.Capacity,
                capacity.MaxConcurrentGoals,
                capacity.ConstraintsJson,
                capacity.ObservedAt,
                capacity.ExpiresAt,
                capacity.Source);

    private static Guid[] ParseDependencies(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Guid[]>(value, JsonOptions) ?? [];
    }

    private static string[] ParseStrings(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(value, JsonOptions) ?? [];
    }
}
