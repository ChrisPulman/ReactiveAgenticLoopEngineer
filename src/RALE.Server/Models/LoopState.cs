namespace RALE.Server.Models;

public sealed record LoopState(
    Guid LoopId,
    string PrimaryPrompt,
    int TokenLimit,
    int ExistingGoalCount,
    IReadOnlyList<string> CompletedOutputs);
