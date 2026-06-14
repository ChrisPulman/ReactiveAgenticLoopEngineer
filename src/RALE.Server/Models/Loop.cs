namespace RALE.Server.Models;

public sealed class Loop
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PrimaryObjective { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public LoopStatus Status { get; set; } = LoopStatus.Pending;

    public int TokenLimit { get; set; }

    public long Version { get; set; }

    public List<Goal> Goals { get; } = [];

    public List<LoopEvent> Events { get; } = [];
}
