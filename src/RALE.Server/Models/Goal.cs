namespace RALE.Server.Models;

public sealed class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LoopId { get; set; }

    public Loop Loop { get; set; } = null!;

    public int Sequence { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string DependsOnJson { get; set; } = "[]";

    public Guid? AssignedAgentId { get; set; }

    public Agent? AssignedAgent { get; set; }

    public string TaskType { get; set; } = "general";

    public int Priority { get; set; }

    public DateTimeOffset? Deadline { get; set; }

    public string RequiredArtifactsJson { get; set; } = "[]";

    public bool ApprovalRequired { get; set; }

    public string ApprovalState { get; set; } = "NotRequired";

    public int IterationLimit { get; set; } = 3;

    public int IterationCount { get; set; }

    public int RetryLimit { get; set; } = 2;

    public int RetryCount { get; set; }

    public string PolicyState { get; set; } = "Allowed";

    public string PolicyViolationsJson { get; set; } = "[]";

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.Pending;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long Version { get; set; }

    public List<GoalResult> Results { get; } = [];
}
