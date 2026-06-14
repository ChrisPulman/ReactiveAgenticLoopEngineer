namespace RALE.Server.Models;

public sealed class GoalResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GoalId { get; set; }

    public Goal Goal { get; set; } = null!;

    public string Output { get; set; } = string.Empty;

    public string Metadata { get; set; } = "{}";

    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
