using RALE.Server.Models;

namespace RALE.Server.Services;

public sealed record AgentCard(
    string Name,
    IReadOnlyList<string> Capabilities,
    string Endpoint,
    int MaxConcurrentGoals,
    int MaxTokenCapacity,
    IReadOnlyList<string> SupportedTaskTypes,
    string Sla,
    string SecurityPosture,
    int TrustLevel,
    IReadOnlyList<string> ToolScopes,
    int CapacityCacheTtlSeconds);

public sealed record AgentCapacity(
    Guid AgentId,
    int Capacity,
    int MaxConcurrentGoals,
    string ConstraintsJson,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string Source);

public sealed record MasterPlanRequest(
    string PrimaryObjective,
    IReadOnlyList<Guid> AgentIds,
    int TokenLimit,
    string TaskType,
    string ExecutionPattern,
    IReadOnlyList<string> RequiredArtifacts,
    string ConstraintsJson,
    int Priority,
    DateTimeOffset? Deadline,
    bool ApprovalRequired,
    int MinTrustLevel,
    IReadOnlyList<string> ToolScopes,
    int IterationLimit,
    int RetryLimit);

public interface IAgentCapacityClient
{
    Task<AgentCapacity?> QueryCapacityAsync(Agent agent, string taskProfile, CancellationToken cancellationToken = default);
}

public interface IOrchestrationEngineer
{
    Task<Agent> RegisterAgentAsync(AgentCard card, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken cancellationToken = default);

    Task<AgentCapacity> DiscoverCapacityAsync(Guid agentId, string taskProfile, CancellationToken cancellationToken = default);

    Task<Loop> CreateMasterPlanAsync(MasterPlanRequest request, CancellationToken cancellationToken = default);

    Task<Goal?> AssignNextGoalAsync(Guid loopId, Guid agentId, CancellationToken cancellationToken = default);

    Task<Goal?> ApproveGoalAsync(Guid goalId, bool approved, string reviewer, CancellationToken cancellationToken = default);

    Task<Goal?> RecordHeartbeatAsync(Guid goalId, string detail, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Goal>> ResplitGoalAsync(Guid goalId, string reason, int? capacityLimit = null, CancellationToken cancellationToken = default);
}
