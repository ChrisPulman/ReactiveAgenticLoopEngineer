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
    LoopCompleted = 9,
    AgentRegistered = 10,
    CapacityDiscovered = 11,
    CapacityFallbackUsed = 12,
    PlanDecomposed = 13,
    GoalAssigned = 14,
    ApprovalRequired = 15,
    GoalApproved = 16,
    GoalRejected = 17,
    GoalHeartbeat = 18,
    GoalResplit = 19,
    PolicyViolation = 20
}
