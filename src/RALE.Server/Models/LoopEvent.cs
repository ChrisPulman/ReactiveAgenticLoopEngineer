namespace RALE.Server.Models;

public sealed class LoopEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LoopId { get; set; }

    public Loop Loop { get; set; } = null!;

    public Guid? GoalId { get; set; }

    public Goal? Goal { get; set; }

    public LoopEventType Type { get; set; }

    public string Detail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
