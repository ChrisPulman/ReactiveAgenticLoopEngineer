namespace RALE.Server.Models;

public sealed class AgentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AgentId { get; set; }

    public Agent Agent { get; set; } = null!;

    public LoopEventType Type { get; set; }

    public string Detail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
