namespace RALE.Server.Models;

public enum LoopEventType
{
    LoopCreated = 0,
    GoalCreated = 1,
    GoalClaimed = 2,
    GoalPaused = 3,
    GoalResumed = 4,
    GoalCompleted = 5,
    GoalFailed = 6,
    LoopPaused = 7,
    LoopResumed = 8,
    LoopCompleted = 9
}
