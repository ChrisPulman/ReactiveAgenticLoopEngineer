namespace RALE.Server.Models;

public sealed class Loop
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PrimaryObjective { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public LoopStatus Status { get; set; } = LoopStatus.Pending;

    public int TokenLimit { get; set; }

    public string ConstraintsJson { get; set; } = "{}";

    public string RequiredArtifactsJson { get; set; } = "[]";

    public int Priority { get; set; }

    public DateTimeOffset? Deadline { get; set; }

    public int IterationLimit { get; set; } = 3;

    public string ExecutionPattern { get; set; } = "serial";

    public long Version { get; set; }

    public List<Goal> Goals { get; } = [];

    public List<LoopEvent> Events { get; } = [];
}
