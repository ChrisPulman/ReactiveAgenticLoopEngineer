namespace RALE.Server.Models;

public sealed class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Capabilities { get; set; } = "[]";

    public string Endpoint { get; set; } = string.Empty;

    public int MaxConcurrentGoals { get; set; } = 1;

    public int MaxTokenCapacity { get; set; } = 4096;

    public string SupportedTaskTypesJson { get; set; } = "[]";

    public string Sla { get; set; } = string.Empty;

    public string SecurityPosture { get; set; } = "unverified";

    public int TrustLevel { get; set; }

    public int CurrentLoad { get; set; }

    public string ToolScopesJson { get; set; } = "[]";

    public int CapacityCacheTtlSeconds { get; set; } = 300;

    public int? CachedCapacity { get; set; }

    public string CachedCapacityConstraintsJson { get; set; } = "{}";

    public DateTimeOffset? CapacityCheckedAt { get; set; }

    public DateTimeOffset? CapacityExpiresAt { get; set; }

    public Guid? AssignedGoalId { get; set; }

    public Goal? AssignedGoal { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public long Version { get; set; }

    public List<AgentEvent> Events { get; } = [];
}
