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

    public GoalStatus Status { get; set; } = GoalStatus.Pending;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long Version { get; set; }

    public List<GoalResult> Results { get; } = [];
}
