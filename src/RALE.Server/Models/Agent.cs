namespace RALE.Server.Models;

public sealed class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Capabilities { get; set; } = "[]";

    public Guid? AssignedGoalId { get; set; }

    public Goal? AssignedGoal { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
