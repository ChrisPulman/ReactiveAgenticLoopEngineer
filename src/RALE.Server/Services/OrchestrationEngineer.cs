using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RALE.Server.Data;
using RALE.Server.Models;

namespace RALE.Server.Services;

public sealed partial class OrchestrationEngineer(
    IDbContextFactory<RALEContext> contextFactory,
    IAgentCapacityClient capacityClient,
    ILogger<OrchestrationEngineer> logger) : IOrchestrationEngineer
{
    private const string ApprovalNotRequired = "NotRequired";
    private const string ApprovalRequired = "Required";
    private const string ApprovalApproved = "Approved";
    private const string ApprovalRejected = "Rejected";
    private const string PolicyAllowed = "Allowed";
    private const string PolicyReviewRequired = "ReviewRequired";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Agent> RegisterAgentAsync(AgentCard card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (string.IsNullOrWhiteSpace(card.Name))
        {
            throw new ArgumentException("Agent name is required.", nameof(card));
        }

        if (card.MaxConcurrentGoals <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(card), card.MaxConcurrentGoals, "Max concurrent goals must be greater than zero.");
        }

        if (card.MaxTokenCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(card), card.MaxTokenCapacity, "Max token capacity must be greater than zero.");
        }

        var agent = new Agent
        {
            Name = card.Name.Trim(),
            Capabilities = Serialize(card.Capabilities),
            Endpoint = card.Endpoint.Trim(),
            MaxConcurrentGoals = card.MaxConcurrentGoals,
            MaxTokenCapacity = card.MaxTokenCapacity,
            SupportedTaskTypesJson = Serialize(card.SupportedTaskTypes),
            Sla = card.Sla.Trim(),
            SecurityPosture = Normalize(card.SecurityPosture, "unverified"),
            TrustLevel = Math.Clamp(card.TrustLevel, 0, 100),
            ToolScopesJson = Serialize(card.ToolScopes),
            CapacityCacheTtlSeconds = Math.Max(1, card.CapacityCacheTtlSeconds),
            CachedCapacity = card.MaxTokenCapacity,
            CachedCapacityConstraintsJson = "{}",
            CapacityCheckedAt = DateTimeOffset.UtcNow,
            CapacityExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, card.CapacityCacheTtlSeconds))
        };

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Agents.Add(agent);
        context.AgentEvents.Add(new AgentEvent
        {
            AgentId = agent.Id,
            Type = LoopEventType.AgentRegistered,
            Detail = $"Registered agent '{agent.Name}' with {agent.MaxConcurrentGoals} concurrent goal slot(s)."
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return agent;
    }

    public async Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Agents
            .AsNoTracking()
            .OrderBy(agent => agent.Name)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AgentCapacity> DiscoverCapacityAsync(Guid agentId, string taskProfile, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var agent = await context.Agents.FirstOrDefaultAsync(existing => existing.Id == agentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Agent '{agentId}' was not found.");

        var live = await capacityClient.QueryCapacityAsync(agent, taskProfile, cancellationToken).ConfigureAwait(false);
        if (live is not null)
        {
            agent.CachedCapacity = live.Capacity;
            agent.MaxConcurrentGoals = live.MaxConcurrentGoals;
            agent.CachedCapacityConstraintsJson = live.ConstraintsJson;
            agent.CapacityCheckedAt = live.ObservedAt;
            agent.CapacityExpiresAt = live.ExpiresAt;
            agent.Version++;
            context.AgentEvents.Add(new AgentEvent
            {
                AgentId = agent.Id,
                Type = LoopEventType.CapacityDiscovered,
                Detail = $"Discovered live capacity {live.Capacity} for task profile '{taskProfile}'."
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return live;
        }

        var now = DateTimeOffset.UtcNow;
        var hasFreshCache = agent.CachedCapacity.HasValue && agent.CapacityExpiresAt is { } expiresAt && expiresAt > now;
        var capacity = Math.Max(1, hasFreshCache ? agent.CachedCapacity!.Value : agent.MaxTokenCapacity);
        var source = hasFreshCache ? "cache" : "profile";
        var observedAt = agent.CapacityCheckedAt ?? now;
        var fallbackExpiresAt = hasFreshCache
            ? agent.CapacityExpiresAt!.Value
            : now.AddSeconds(Math.Max(1, agent.CapacityCacheTtlSeconds));

        if (!hasFreshCache)
        {
            agent.CachedCapacity = capacity;
            agent.CachedCapacityConstraintsJson = "{}";
            agent.CapacityCheckedAt = now;
            agent.CapacityExpiresAt = fallbackExpiresAt;
            agent.Version++;
        }

        context.AgentEvents.Add(new AgentEvent
        {
            AgentId = agent.Id,
            Type = LoopEventType.CapacityFallbackUsed,
            Detail = $"Used {source} capacity {capacity} for task profile '{taskProfile}'."
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        UsingFallbackCapacity(logger, source, agentId);
        return new AgentCapacity(
            agent.Id,
            capacity,
            Math.Max(1, agent.MaxConcurrentGoals),
            agent.CachedCapacityConstraintsJson,
            observedAt,
            fallbackExpiresAt,
            source);
    }

    public async Task<Loop> CreateMasterPlanAsync(MasterPlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PrimaryObjective))
        {
            throw new ArgumentException("Primary objective is required.", nameof(request));
        }

        if (request.AgentIds.Count == 0)
        {
            throw new ArgumentException("At least one agent id is required.", nameof(request));
        }

        if (request.TokenLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.TokenLimit, "Token limit must be greater than zero.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var agents = await context.Agents
            .Where(agent => request.AgentIds.Contains(agent.Id))
            .OrderBy(agent => agent.CurrentLoad)
            .ThenBy(agent => agent.Name)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (agents.Length != request.AgentIds.Count)
        {
            throw new InvalidOperationException("One or more requested agents were not found.");
        }

        var capacities = new List<(Agent Agent, AgentCapacity Capacity, List<string> Violations)>(agents.Length);
        foreach (var agent in agents)
        {
            var capacity = await DiscoverCapacityAsync(agent.Id, request.TaskType, cancellationToken).ConfigureAwait(false);
            capacities.Add((agent, capacity, EvaluatePolicy(agent, request)));
        }

        var loop = new Loop
        {
            PrimaryObjective = request.PrimaryObjective.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = LoopStatus.Running,
            TokenLimit = request.TokenLimit,
            ConstraintsJson = string.IsNullOrWhiteSpace(request.ConstraintsJson) ? "{}" : request.ConstraintsJson.Trim(),
            RequiredArtifactsJson = Serialize(request.RequiredArtifacts),
            Priority = request.Priority,
            Deadline = request.Deadline,
            IterationLimit = Math.Max(1, request.IterationLimit),
            ExecutionPattern = NormalizePattern(request.ExecutionPattern)
        };

        var goals = BuildCapacityFitGoals(loop, request, capacities);
        loop.Goals.AddRange(goals);
        loop.Events.Add(new LoopEvent
        {
            LoopId = loop.Id,
            Type = LoopEventType.LoopCreated,
            Detail = $"Created master plan with {goals.Count} capacity-fit task(s)."
        });
        loop.Events.Add(new LoopEvent
        {
            LoopId = loop.Id,
            Type = LoopEventType.PlanDecomposed,
            Detail = $"Execution pattern '{loop.ExecutionPattern}' assigned to {capacities.Count} agent(s)."
        });

        foreach (var goal in goals)
        {
            loop.Events.Add(new LoopEvent
            {
                LoopId = loop.Id,
                GoalId = goal.Id,
                Type = LoopEventType.GoalCreated,
                Detail = goal.Description
            });

            if (goal.PolicyState != PolicyAllowed)
            {
                loop.Events.Add(new LoopEvent
                {
                    LoopId = loop.Id,
                    GoalId = goal.Id,
                    Type = LoopEventType.PolicyViolation,
                    Detail = goal.PolicyViolationsJson
                });
            }

            if (goal.ApprovalRequired)
            {
                loop.Events.Add(new LoopEvent
                {
                    LoopId = loop.Id,
                    GoalId = goal.Id,
                    Type = LoopEventType.ApprovalRequired,
                    Detail = $"Goal {goal.Sequence} requires approval before dispatch."
                });
            }
        }

        context.Loops.Add(loop);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return loop;
    }

    public async Task<Goal?> AssignNextGoalAsync(Guid loopId, Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var agent = await context.Agents.FirstOrDefaultAsync(existing => existing.Id == agentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return null;
        }

        var runningCount = await context.Goals
            .CountAsync(goal => goal.AssignedAgentId == agentId && goal.Status == GoalStatus.InProgress, cancellationToken)
            .ConfigureAwait(false);

        if (runningCount >= Math.Max(1, agent.MaxConcurrentGoals))
        {
            return null;
        }

        var goals = await context.Goals
            .Include(goal => goal.Loop)
            .Where(goal => goal.LoopId == loopId
                && goal.Status == GoalStatus.Pending
                && (goal.AssignedAgentId == null || goal.AssignedAgentId == agentId))
            .OrderByDescending(goal => goal.Priority)
            .ThenBy(goal => goal.Sequence)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var goal in goals)
        {
            if (goal.Loop.Status != LoopStatus.Running
                || goal.PolicyState != PolicyAllowed
                || (goal.ApprovalRequired && goal.ApprovalState != ApprovalApproved)
                || !await DependenciesCompleteAsync(context, goal, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            goal.AssignedAgentId ??= agent.Id;
            goal.Status = GoalStatus.InProgress;
            goal.StartedAt = DateTimeOffset.UtcNow;
            goal.Version++;
            agent.AssignedGoalId = goal.Id;
            agent.CurrentLoad = runningCount + 1;
            agent.Version++;
            context.LoopEvents.Add(new LoopEvent
            {
                LoopId = goal.LoopId,
                GoalId = goal.Id,
                Type = LoopEventType.GoalAssigned,
                Detail = $"Goal {goal.Sequence} assigned to agent {agent.Name}."
            });

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await context.Goals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(existing => existing.Id == goal.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                AssignmentRaceLost(logger, agentId, goal.Id);
                return null;
            }
        }

        return null;
    }

    public async Task<Goal?> ApproveGoalAsync(Guid goalId, bool approved, string reviewer, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals.FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return null;
        }

        goal.ApprovalRequired = !approved;
        goal.ApprovalState = approved ? ApprovalApproved : ApprovalRejected;
        goal.PolicyState = approved ? PolicyAllowed : PolicyReviewRequired;
        goal.Status = approved && goal.Status == GoalStatus.Paused ? GoalStatus.Pending : goal.Status;
        goal.Version++;
        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = approved ? LoopEventType.GoalApproved : LoopEventType.GoalRejected,
            Detail = $"{(approved ? "Approved" : "Rejected")} by {Normalize(reviewer, "unknown reviewer")}."
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

    public async Task<Goal?> RecordHeartbeatAsync(Guid goalId, string detail, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals.FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return null;
        }

        goal.LastHeartbeatAt = DateTimeOffset.UtcNow;
        goal.Version++;
        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = LoopEventType.GoalHeartbeat,
            Detail = string.IsNullOrWhiteSpace(detail) ? "Agent heartbeat." : detail.Trim()
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

    public async Task<IReadOnlyList<Goal>> ResplitGoalAsync(Guid goalId, string reason, int? capacityLimit = null, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals
            .Include(existing => existing.Loop)
            .FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken)
            .ConfigureAwait(false);

        if (goal is null)
        {
            return [];
        }

        goal.IterationCount++;
        if (goal.IterationCount > Math.Max(1, goal.IterationLimit))
        {
            goal.ApprovalRequired = true;
            goal.ApprovalState = ApprovalRequired;
            goal.PolicyState = PolicyReviewRequired;
            goal.Status = GoalStatus.Paused;
            goal.Version++;
            context.LoopEvents.Add(new LoopEvent
            {
                LoopId = goal.LoopId,
                GoalId = goal.Id,
                Type = LoopEventType.ApprovalRequired,
                Detail = $"Iteration limit reached while re-splitting: {Normalize(reason, "capacity mismatch")}."
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return [];
        }

        var capacity = await ResolveGoalCapacityAsync(context, goal, capacityLimit, cancellationToken).ConfigureAwait(false);
        var drafts = PromptDecomposer.Decompose(goal.Prompt, Math.Max(1, capacity));
        if (drafts.Count <= 1)
        {
            goal.Status = GoalStatus.Pending;
            goal.Version++;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return [goal];
        }

        var downstreamGoals = await FindDependentGoalsAsync(context, goal.LoopId, goal.Id, cancellationToken).ConfigureAwait(false);
        var maxSequence = await context.Goals
            .Where(existing => existing.LoopId == goal.LoopId)
            .MaxAsync(existing => existing.Sequence, cancellationToken)
            .ConfigureAwait(false);

        var replacements = new List<Goal>(drafts.Count);
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var replacement = new Goal
            {
                LoopId = goal.LoopId,
                Sequence = maxSequence + index + 1,
                Description = $"Replacement {index + 1} for goal {goal.Sequence}: {draft.Prompt}",
                Prompt = draft.Prompt,
                DependsOnJson = index == 0
                    ? goal.DependsOnJson
                    : Serialize(new[] { replacements[index - 1].Id }),
                AssignedAgentId = goal.AssignedAgentId,
                TaskType = goal.TaskType,
                Priority = goal.Priority,
                Deadline = goal.Deadline,
                RequiredArtifactsJson = goal.RequiredArtifactsJson,
                ApprovalRequired = goal.ApprovalRequired,
                ApprovalState = goal.ApprovalState,
                IterationLimit = goal.IterationLimit,
                RetryLimit = goal.RetryLimit,
                PolicyState = goal.PolicyState,
                PolicyViolationsJson = goal.PolicyViolationsJson
            };
            replacements.Add(replacement);
        }

        var finalReplacementId = replacements[^1].Id;
        foreach (var downstream in downstreamGoals)
        {
            var dependencies = ParseDependencies(downstream.DependsOnJson)
                .Select(id => id == goal.Id ? finalReplacementId : id)
                .Distinct()
                .ToArray();
            downstream.DependsOnJson = Serialize(dependencies);
            downstream.Version++;
        }

        goal.Status = GoalStatus.Skipped;
        goal.Version++;
        context.Goals.AddRange(replacements);
        context.LoopEvents.Add(new LoopEvent
        {
            LoopId = goal.LoopId,
            GoalId = goal.Id,
            Type = LoopEventType.GoalResplit,
            Detail = $"Goal {goal.Sequence} re-split into {replacements.Count} replacement task(s): {Normalize(reason, "capacity mismatch")}."
        });
        foreach (var replacement in replacements)
        {
            context.LoopEvents.Add(new LoopEvent
            {
                LoopId = replacement.LoopId,
                GoalId = replacement.Id,
                Type = LoopEventType.GoalCreated,
                Detail = replacement.Description
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return replacements;
    }

    private static List<Goal> BuildCapacityFitGoals(
        Loop loop,
        MasterPlanRequest request,
        List<(Agent Agent, AgentCapacity Capacity, List<string> Violations)> capacities)
    {
        var words = NonWhitespace().Matches(request.PrimaryObjective.Trim()).Select(match => match.Value).ToList();
        var goals = new List<Goal>();
        var wordIndex = 0;
        Guid? previousGoalId = null;
        var sequence = 1;
        var isSerial = NormalizePattern(request.ExecutionPattern) == "serial";

        while (wordIndex < words.Count)
        {
            var candidate = capacities[(sequence - 1) % capacities.Count];
            var promptLimit = Math.Max(1, Math.Min(request.TokenLimit, candidate.Capacity.Capacity));
            var prompt = TakePrompt(words, ref wordIndex, promptLimit);
            var approvalRequired = request.ApprovalRequired || candidate.Violations.Count > 0;
            var policyState = candidate.Violations.Count == 0 ? PolicyAllowed : PolicyReviewRequired;
            var approvalState = approvalRequired ? ApprovalRequired : ApprovalNotRequired;
            var goal = new Goal
            {
                LoopId = loop.Id,
                Sequence = sequence,
                Description = $"Task {sequence} for {candidate.Agent.Name}: {CreatePreview(prompt)}",
                Prompt = prompt,
                DependsOnJson = isSerial && previousGoalId.HasValue ? Serialize(new[] { previousGoalId.Value }) : "[]",
                AssignedAgentId = candidate.Agent.Id,
                TaskType = Normalize(request.TaskType, "general"),
                Priority = request.Priority,
                Deadline = request.Deadline,
                RequiredArtifactsJson = Serialize(request.RequiredArtifacts),
                ApprovalRequired = approvalRequired,
                ApprovalState = approvalState,
                IterationLimit = Math.Max(1, request.IterationLimit),
                RetryLimit = Math.Max(0, request.RetryLimit),
                PolicyState = policyState,
                PolicyViolationsJson = Serialize(candidate.Violations)
            };

            goals.Add(goal);
            previousGoalId = goal.Id;
            sequence++;
        }

        return goals;
    }

    private static List<string> EvaluatePolicy(Agent agent, MasterPlanRequest request)
    {
        var violations = new List<string>();
        if (agent.TrustLevel < request.MinTrustLevel)
        {
            violations.Add($"Agent trust level {agent.TrustLevel} is below required {request.MinTrustLevel}.");
        }

        var supportedTaskTypes = ParseStrings(agent.SupportedTaskTypesJson);
        if (supportedTaskTypes.Length > 0
            && !supportedTaskTypes.Contains(request.TaskType, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"Agent does not declare support for task type '{request.TaskType}'.");
        }

        var availableScopes = ParseStrings(agent.ToolScopesJson).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var scope in request.ToolScopes)
        {
            if (!availableScopes.Contains(scope))
            {
                violations.Add($"Agent lacks required tool scope '{scope}'.");
            }
        }

        return violations;
    }

    private static async Task<bool> DependenciesCompleteAsync(RALEContext context, Goal goal, CancellationToken cancellationToken)
    {
        var dependencies = ParseDependencies(goal.DependsOnJson);
        if (dependencies.Length == 0)
        {
            return true;
        }

        var completedCount = await context.Goals
            .AsNoTracking()
            .CountAsync(existing => dependencies.Contains(existing.Id) && existing.Status == GoalStatus.Complete, cancellationToken)
            .ConfigureAwait(false);

        return completedCount == dependencies.Length;
    }

    private static async Task<IReadOnlyList<Goal>> FindDependentGoalsAsync(
        RALEContext context,
        Guid loopId,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        var goals = await context.Goals
            .Where(goal => goal.LoopId == loopId && goal.Id != goalId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. goals.Where(goal => ParseDependencies(goal.DependsOnJson).Contains(goalId))];
    }

    private static async Task<int> ResolveGoalCapacityAsync(
        RALEContext context,
        Goal goal,
        int? capacityLimit,
        CancellationToken cancellationToken)
    {
        if (capacityLimit is > 0)
        {
            return capacityLimit.Value;
        }

        if (!goal.AssignedAgentId.HasValue)
        {
            return Math.Max(1, goal.Loop.TokenLimit / 2);
        }

        var agent = await context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.Id == goal.AssignedAgentId.Value, cancellationToken)
            .ConfigureAwait(false);

        var capacity = agent?.CachedCapacity ?? agent?.MaxTokenCapacity ?? goal.Loop.TokenLimit;
        return Math.Max(1, Math.Min(goal.Loop.TokenLimit, capacity));
    }

    private static string TakePrompt(List<string> words, ref int wordIndex, int promptLimit)
    {
        var current = string.Empty;
        while (wordIndex < words.Count)
        {
            var word = words[wordIndex];
            if (word.Length > promptLimit && current.Length == 0)
            {
                words[wordIndex] = word[promptLimit..];
                return word[..promptLimit];
            }

            var candidateLength = current.Length == 0 ? word.Length : current.Length + 1 + word.Length;
            if (candidateLength > promptLimit)
            {
                break;
            }

            current = current.Length == 0 ? word : $"{current} {word}";
            wordIndex++;
        }

        if (current.Length > 0)
        {
            return current;
        }

        var fallback = words[wordIndex];
        wordIndex++;
        return fallback.Length <= promptLimit ? fallback : fallback[..promptLimit];
    }

    private static Guid[] ParseDependencies(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Guid[]>(value, JsonOptions) ?? [];
    }

    private static string[] ParseStrings(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(value, JsonOptions) ?? [];
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string NormalizePattern(string value) =>
        string.Equals(value, "parallel", StringComparison.OrdinalIgnoreCase) ? "parallel" : "serial";

    private static string Normalize(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string CreatePreview(string prompt) =>
        prompt.Length <= 80 ? prompt : string.Concat(prompt.AsSpan(0, 77), "...");

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Using {Source} capacity for agent {AgentId}.")]
    private static partial void UsingFallbackCapacity(ILogger logger, string source, Guid agentId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Agent {AgentId} lost an assignment race for goal {GoalId}.")]
    private static partial void AssignmentRaceLost(ILogger logger, Guid agentId, Guid goalId);

    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWhitespace();
}
