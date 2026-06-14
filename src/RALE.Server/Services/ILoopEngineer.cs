using RALE.Server.Models;

namespace RALE.Server.Services;

public interface ILoopEngineer
{
    Task<Loop> CreateLoop(string primaryPrompt, int tokenLimit, CancellationToken cancellationToken = default);

    IObservable<Goal> ObserveNextGoals(Guid loopId);

    Task<Goal> Decompose(LoopState state, CancellationToken cancellationToken = default);

    Task UpdateWithResult(GoalResult result, CancellationToken cancellationToken = default);

    Task<Loop?> GetLoopAsync(Guid loopId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Goal>> ListGoalsAsync(Guid loopId, CancellationToken cancellationToken = default);

    Task<Goal?> ClaimNextGoalAsync(Guid loopId, CancellationToken cancellationToken = default);

    Task<bool> TryClaimGoalAsync(Guid goalId, CancellationToken cancellationToken = default);

    Task<Goal?> PauseGoalAsync(Guid goalId, CancellationToken cancellationToken = default);

    Task<Goal?> ResumeGoalAsync(Guid goalId, CancellationToken cancellationToken = default);

    Task<GoalResult> CompleteGoalAsync(Guid goalId, string output, string? metadata = null, CancellationToken cancellationToken = default);

    Task<Goal?> FailGoalAsync(Guid goalId, string reason, CancellationToken cancellationToken = default);
}
