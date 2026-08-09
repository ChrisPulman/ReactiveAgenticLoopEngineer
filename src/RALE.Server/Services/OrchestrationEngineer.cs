// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RALE.Server.Data;
using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Coordinates agent capacity, plan decomposition, assignment, and approval workflows.</summary>
/// <param name="contextFactory">The database context factory.</param>
/// <param name="capacityClient">The client used to discover live agent capacity.</param>
/// <param name="timeProvider">The provider for current timestamps.</param>
/// <param name="logger">The logger for orchestration diagnostics.</param>
public sealed partial class OrchestrationEngineer(
    IDbContextFactory<RALEContext> contextFactory,
    IAgentCapacityClient capacityClient,
    TimeProvider timeProvider,
    ILogger<OrchestrationEngineer> logger) : IOrchestrationEngineer
{
    /// <summary>The minimum supported trust level.</summary>
    private const int MinimumTrustLevel = 0;

    /// <summary>The maximum supported trust level.</summary>
    private const int MaximumTrustLevel = 100;

    /// <summary>The approval state used when approval is not required.</summary>
    private const string ApprovalNotRequired = "NotRequired";

    /// <summary>The approval state used when approval is required.</summary>
    private const string ApprovalRequired = "Required";

    /// <summary>The approval state used after approval.</summary>
    private const string ApprovalApproved = "Approved";

    /// <summary>The approval state used after rejection.</summary>
    private const string ApprovalRejected = "Rejected";

    /// <summary>The policy state used for work that may be dispatched.</summary>
    private const string PolicyAllowed = "Allowed";

    /// <summary>The policy state used for work that requires review.</summary>
    private const string PolicyReviewRequired = "ReviewRequired";

    /// <summary>The shared JSON serializer configuration.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<Agent> RegisterAgentAsync(AgentCard card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        ArgumentException.ThrowIfNullOrWhiteSpace(card.Name, nameof(card));

        if (card.MaxConcurrentGoals <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(card), card.MaxConcurrentGoals, "Max concurrent goals must be greater than zero.");
        }

        if (card.MaxTokenCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(card), card.MaxTokenCapacity, "Max token capacity must be greater than zero.");
        }

        var now = timeProvider.GetUtcNow();
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
            TrustLevel = Math.Clamp(card.TrustLevel, MinimumTrustLevel, MaximumTrustLevel),
            ToolScopesJson = Serialize(card.ToolScopes),
            CapacityCacheTtlSeconds = Math.Max(1, card.CapacityCacheTtlSeconds),
            CachedCapacity = card.MaxTokenCapacity,
            CachedCapacityConstraintsJson = "{}",
            CapacityCheckedAt = now,
            CapacityExpiresAt = now.AddSeconds(Math.Max(1, card.CapacityCacheTtlSeconds))
        };

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        _ = await context.Agents.AddAsync(agent, cancellationToken).ConfigureAwait(false);
        _ = await context.AgentEvents.AddAsync(
            new AgentEvent { AgentId = agent.Id, Type = LoopEventType.AgentRegistered, Detail = $"Registered agent '{agent.Name}' with {agent.MaxConcurrentGoals} concurrent goal slot(s)." },
            cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return agent;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Agents
            .AsNoTracking()
            .OrderBy(agent => agent.Name)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
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
            _ = await context.AgentEvents.AddAsync(
                new AgentEvent { AgentId = agent.Id, Type = LoopEventType.CapacityDiscovered, Detail = $"Discovered live capacity {live.Capacity} for task profile '{taskProfile}'." },
                cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return live;
        }

        var now = timeProvider.GetUtcNow();
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

        _ = await context.AgentEvents.AddAsync(
            new AgentEvent { AgentId = agent.Id, Type = LoopEventType.CapacityFallbackUsed, Detail = $"Used {source} capacity {capacity} for task profile '{taskProfile}'." },
            cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        UsingFallbackCapacity(logger, source, agentId);
        return new(
            agent.Id,
            capacity,
            Math.Max(1, agent.MaxConcurrentGoals),
            agent.CachedCapacityConstraintsJson,
            observedAt,
            fallbackExpiresAt,
            source);
    }

    /// <inheritdoc />
    public async Task<Loop> CreateMasterPlanAsync(MasterPlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ArgumentException.ThrowIfNullOrWhiteSpace(request.PrimaryObjective, nameof(request));

        if (request.AgentIds.Count == 0)
        {
            throw new ArgumentException("At least one agent id is required.", nameof(request));
        }

        if (request.TokenLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.TokenLimit, "Token limit must be greater than zero.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var agents = new List<Agent>(request.AgentIds.Count);
        var requestedAgentIds = new HashSet<Guid>();
        foreach (var agentId in request.AgentIds)
        {
            if (!requestedAgentIds.Add(agentId))
            {
                continue;
            }

            var agent = await context.Agents
                .FirstOrDefaultAsync(existing => existing.Id == agentId, cancellationToken)
                .ConfigureAwait(false);
            if (agent is not null)
            {
                agents.Add(agent);
            }
        }

        agents.Sort(static (left, right) =>
        {
            var loadComparison = left.CurrentLoad.CompareTo(right.CurrentLoad);
            return loadComparison != 0 ? loadComparison : string.CompareOrdinal(left.Name, right.Name);
        });

        if (agents.Count != request.AgentIds.Count)
        {
            throw new InvalidOperationException("One or more requested agents were not found.");
        }

        var capacities = new List<(Agent Agent, AgentCapacity Capacity, List<string> Violations)>(agents.Count);
        foreach (var agent in agents)
        {
            var capacity = await DiscoverCapacityAsync(agent.Id, request.TaskType, cancellationToken).ConfigureAwait(false);
            capacities.Add((agent, capacity, EvaluatePolicy(agent, request)));
        }

        var loop = CreatePlanLoop(request, timeProvider);
        var goals = BuildCapacityFitGoals(loop, request, capacities);
        loop.Goals.AddRange(goals);
        AddPlanEvents(loop, goals, capacities.Count);
        _ = await context.Loops.AddAsync(loop, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return loop;
    }

    /// <inheritdoc />
    public async Task<Goal?> AssignNextGoalAsync(Guid loopId, Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var agent = await context.Agents.FirstOrDefaultAsync(existing => existing.Id == agentId, cancellationToken).ConfigureAwait(false);
        if (agent is null) return null;

        var runningCount = await context.Goals
            .CountAsync(goal => goal.AssignedAgentId == agentId && goal.Status == GoalStatus.InProgress, cancellationToken)
            .ConfigureAwait(false);

        if (runningCount >= Math.Max(1, agent.MaxConcurrentGoals)) return null;

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
            goal.StartedAt = timeProvider.GetUtcNow();
            goal.Version++;
            agent.AssignedGoalId = goal.Id;
            agent.CurrentLoad = runningCount + 1;
            agent.Version++;
            _ = await context.LoopEvents.AddAsync(
                new LoopEvent { LoopId = goal.LoopId, GoalId = goal.Id, Type = LoopEventType.GoalAssigned, Detail = $"Goal {goal.Sequence} assigned to agent {agent.Name}." },
                cancellationToken).ConfigureAwait(false);

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

    /// <inheritdoc />
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
        _ = await context.LoopEvents.AddAsync(
            new LoopEvent
            {
                LoopId = goal.LoopId,
                GoalId = goal.Id,
                Type = approved ? LoopEventType.GoalApproved : LoopEventType.GoalRejected,
                Detail = $"{(approved ? "Approved" : "Rejected")} by {Normalize(reviewer, "unknown reviewer")}."
            },
            cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

    /// <inheritdoc />
    public async Task<Goal?> RecordHeartbeatAsync(Guid goalId, string detail, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var goal = await context.Goals.FirstOrDefaultAsync(existing => existing.Id == goalId, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return null;
        }

        goal.LastHeartbeatAt = timeProvider.GetUtcNow();
        goal.Version++;
        _ = await context.LoopEvents.AddAsync(
            new LoopEvent { LoopId = goal.LoopId, GoalId = goal.Id, Type = LoopEventType.GoalHeartbeat, Detail = string.IsNullOrWhiteSpace(detail) ? "Agent heartbeat." : detail.Trim() },
            cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return goal;
    }

    /// <inheritdoc />
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
        if (await PauseAtIterationLimitAsync(context, goal, reason, cancellationToken).ConfigureAwait(false))
        {
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

        var replacements = CreateReplacementGoals(goal, drafts, maxSequence);

        var finalReplacementId = replacements[^1].Id;
        ReplaceDependency(downstreamGoals, goal.Id, finalReplacementId);

        goal.Status = GoalStatus.Skipped;
        goal.Version++;
        await context.Goals.AddRangeAsync(replacements, cancellationToken).ConfigureAwait(false);
        await AddResplitEventsAsync(context, goal, reason, replacements, cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return replacements;
    }

    /// <summary>Pauses re-splitting when a goal has exceeded its iteration limit.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="goal">The goal being re-split.</param>
    /// <param name="reason">The re-split reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the goal was paused.</returns>
    private static async Task<bool> PauseAtIterationLimitAsync(RALEContext context, Goal goal, string reason, CancellationToken cancellationToken)
    {
        if (goal.IterationCount <= Math.Max(1, goal.IterationLimit))
        {
            return false;
        }

        goal.ApprovalRequired = true;
        goal.ApprovalState = ApprovalRequired;
        goal.PolicyState = PolicyReviewRequired;
        goal.Status = GoalStatus.Paused;
        goal.Version++;
        _ = await context.LoopEvents.AddAsync(
            new LoopEvent
            {
                LoopId = goal.LoopId,
                GoalId = goal.Id,
                Type = LoopEventType.ApprovalRequired,
                Detail = $"Iteration limit reached while re-splitting: {Normalize(reason, "capacity mismatch")}."
            },
            cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Creates replacement goals from bounded drafts.</summary>
    /// <param name="goal">The goal being replaced.</param>
    /// <param name="drafts">The replacement drafts.</param>
    /// <param name="maxSequence">The highest existing sequence.</param>
    /// <returns>The replacement goals.</returns>
    private static List<Goal> CreateReplacementGoals(Goal goal, IReadOnlyList<GoalDraft> drafts, int maxSequence)
    {
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
                DependsOnJson = index == 0 ? goal.DependsOnJson : Serialize(new[] { replacements[index - 1].Id }),
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

        return replacements;
    }

    /// <summary>Creates a persisted loop skeleton from a planning request.</summary>
    /// <param name="request">The planning request.</param>
    /// <param name="timeProvider">The source of the loop creation timestamp.</param>
    /// <returns>The new loop.</returns>
    private static Loop CreatePlanLoop(MasterPlanRequest request, TimeProvider timeProvider) => new()
    {
        PrimaryObjective = request.PrimaryObjective.Trim(),
        CreatedAt = timeProvider.GetUtcNow(),
        Status = LoopStatus.Running,
        TokenLimit = request.TokenLimit,
        ConstraintsJson = string.IsNullOrWhiteSpace(request.ConstraintsJson) ? "{}" : request.ConstraintsJson.Trim(),
        RequiredArtifactsJson = Serialize(request.RequiredArtifacts),
        Priority = request.Priority,
        Deadline = request.Deadline,
        IterationLimit = Math.Max(1, request.IterationLimit),
        ExecutionPattern = NormalizePattern(request.ExecutionPattern)
    };

    /// <summary>Adds lifecycle events for an initial plan and its goals.</summary>
    /// <param name="loop">The new loop.</param>
    /// <param name="goals">The plan goals.</param>
    /// <param name="agentCount">The number of participating agents.</param>
    private static void AddPlanEvents(Loop loop, List<Goal> goals, int agentCount)
    {
        loop.Events.Add(new LoopEvent { LoopId = loop.Id, Type = LoopEventType.LoopCreated, Detail = $"Created master plan with {goals.Count} capacity-fit task(s)." });
        loop.Events.Add(new LoopEvent { LoopId = loop.Id, Type = LoopEventType.PlanDecomposed, Detail = $"Execution pattern '{loop.ExecutionPattern}' assigned to {agentCount} agent(s)." });

        foreach (var goal in goals)
        {
            loop.Events.Add(new LoopEvent { LoopId = loop.Id, GoalId = goal.Id, Type = LoopEventType.GoalCreated, Detail = goal.Description });
            if (goal.PolicyState != PolicyAllowed)
            {
                loop.Events.Add(new LoopEvent { LoopId = loop.Id, GoalId = goal.Id, Type = LoopEventType.PolicyViolation, Detail = goal.PolicyViolationsJson });
            }

            if (goal.ApprovalRequired)
            {
                loop.Events.Add(new LoopEvent { LoopId = loop.Id, GoalId = goal.Id, Type = LoopEventType.ApprovalRequired, Detail = $"Goal {goal.Sequence} requires approval before dispatch." });
            }
        }
    }

    /// <summary>Rewrites downstream dependencies to point to a final replacement goal.</summary>
    /// <param name="goals">The downstream goals.</param>
    /// <param name="replacedGoalId">The replaced goal identifier.</param>
    /// <param name="replacementGoalId">The replacement goal identifier.</param>
    private static void ReplaceDependency(IReadOnlyList<Goal> goals, Guid replacedGoalId, Guid replacementGoalId)
    {
        foreach (var goal in goals)
        {
            var dependencies = ParseDependencies(goal.DependsOnJson);
            var uniqueDependencies = new List<Guid>(dependencies.Length);
            foreach (var dependency in dependencies)
            {
                var replacement = dependency == replacedGoalId ? replacementGoalId : dependency;
                if (!uniqueDependencies.Contains(replacement))
                {
                    uniqueDependencies.Add(replacement);
                }
            }

            goal.DependsOnJson = Serialize(uniqueDependencies);
            goal.Version++;
        }
    }

    /// <summary>Adds events describing a goal re-split and its replacements.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="goal">The replaced goal.</param>
    /// <param name="reason">The re-split reason.</param>
    /// <param name="replacements">The replacement goals.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the event persistence operation.</returns>
    private static async Task AddResplitEventsAsync(RALEContext context, Goal goal, string reason, List<Goal> replacements, CancellationToken cancellationToken)
    {
        _ = await context.LoopEvents.AddAsync(
            new LoopEvent
            {
                LoopId = goal.LoopId,
                GoalId = goal.Id,
                Type = LoopEventType.GoalResplit,
                Detail = $"Goal {goal.Sequence} re-split into {replacements.Count} replacement task(s): {Normalize(reason, "capacity mismatch")}."
            },
            cancellationToken).ConfigureAwait(false);
        foreach (var replacement in replacements)
        {
            _ = await context.LoopEvents.AddAsync(
                new LoopEvent { LoopId = replacement.LoopId, GoalId = replacement.Id, Type = LoopEventType.GoalCreated, Detail = replacement.Description },
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Creates capacity-bounded goals for a planning request.</summary>
    /// <param name="loop">The new loop.</param>
    /// <param name="request">The planning request.</param>
    /// <param name="capacities">The available agent capacities.</param>
    /// <returns>The capacity-bounded goals.</returns>
    private static List<Goal> BuildCapacityFitGoals(
        Loop loop,
        MasterPlanRequest request,
        List<(Agent Agent, AgentCapacity Capacity, List<string> Violations)> capacities)
    {
        var matches = NonWhitespace().Matches(request.PrimaryObjective.Trim());
        var words = new List<string>(matches.Count);
        for (var matchIndex = 0; matchIndex < matches.Count; matchIndex++)
        {
            words.Add(matches[matchIndex].Value);
        }

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

    /// <summary>Evaluates whether an agent satisfies request policy constraints.</summary>
    /// <param name="agent">The agent to evaluate.</param>
    /// <param name="request">The planning request.</param>
    /// <returns>The policy violations.</returns>
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

        var availableScopes = new HashSet<string>(ParseStrings(agent.ToolScopesJson), StringComparer.OrdinalIgnoreCase);
        foreach (var scope in request.ToolScopes)
        {
            if (!availableScopes.Contains(scope))
            {
                violations.Add($"Agent lacks required tool scope '{scope}'.");
            }
        }

        return violations;
    }

    /// <summary>Determines whether all stored dependencies of a goal have completed.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="goal">The goal to inspect.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when all dependencies are complete.</returns>
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

    /// <summary>Finds goals that reference a particular predecessor.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="loopId">The containing loop identifier.</param>
    /// <param name="goalId">The predecessor goal identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dependent goals.</returns>
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

        var dependentGoals = new List<Goal>();
        foreach (var goal in goals)
        {
            foreach (var dependency in ParseDependencies(goal.DependsOnJson))
            {
                if (dependency == goalId)
                {
                    dependentGoals.Add(goal);
                    break;
                }
            }
        }

        return dependentGoals;
    }

    /// <summary>Resolves the capacity available to a goal.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="goal">The goal to inspect.</param>
    /// <param name="capacityLimit">An explicit capacity limit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved capacity.</returns>
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
            const int UnassignedGoalCapacityDivisor = 2;
            return Math.Max(1, goal.Loop.TokenLimit / UnassignedGoalCapacityDivisor);
        }

        var agent = await context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.Id == goal.AssignedAgentId.Value, cancellationToken)
            .ConfigureAwait(false);

        var capacity = agent?.CachedCapacity ?? agent?.MaxTokenCapacity ?? goal.Loop.TokenLimit;
        return Math.Max(1, Math.Min(goal.Loop.TokenLimit, capacity));
    }

    /// <summary>Takes the next capacity-bounded prompt segment.</summary>
    /// <param name="words">The remaining prompt words.</param>
    /// <param name="wordIndex">The index of the next word.</param>
    /// <param name="promptLimit">The maximum prompt length.</param>
    /// <returns>The next bounded prompt.</returns>
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

    /// <summary>Parses a serialized dependency list.</summary>
    /// <param name="value">The serialized dependency list.</param>
    /// <returns>The dependency identifiers.</returns>
    private static Guid[] ParseDependencies(string value) => string.IsNullOrWhiteSpace(value)
        ? []
        : JsonSerializer.Deserialize<Guid[]>(value, JsonOptions) ?? [];

    /// <summary>Parses a serialized string list.</summary>
    /// <param name="value">The serialized string list.</param>
    /// <returns>The parsed strings.</returns>
    private static string[] ParseStrings(string value) => string.IsNullOrWhiteSpace(value)
        ? []
        : JsonSerializer.Deserialize<string[]>(value, JsonOptions) ?? [];

    /// <summary>Serializes a value using the server JSON configuration.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The serialized JSON.</returns>
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    /// <summary>Normalizes a requested execution pattern.</summary>
    /// <param name="value">The requested execution pattern.</param>
    /// <returns>The normalized execution pattern.</returns>
    private static string NormalizePattern(string value) =>
        string.Equals(value, "parallel", StringComparison.OrdinalIgnoreCase) ? "parallel" : "serial";

    /// <summary>Returns a trimmed value or its fallback.</summary>
    /// <param name="value">The value to normalize.</param>
    /// <param name="fallback">The fallback value.</param>
    /// <returns>The normalized value.</returns>
    private static string Normalize(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    /// <summary>Creates a compact prompt preview.</summary>
    /// <param name="prompt">The prompt to preview.</param>
    /// <returns>The compact preview.</returns>
    private static string CreatePreview(string prompt)
    {
        const int MaximumPreviewLength = 80;
        const int PreviewContentLength = MaximumPreviewLength - 3;
        return prompt.Length <= MaximumPreviewLength ? prompt : string.Concat(prompt.AsSpan(0, PreviewContentLength), "...");
    }

    /// <summary>Logs use of cached or profile capacity.</summary>
    /// <param name="logger">The logger receiving the diagnostic.</param>
    /// <param name="source">The capacity source.</param>
    /// <param name="agentId">The agent identifier.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Using {Source} capacity for agent {AgentId}.")]
    private static partial void UsingFallbackCapacity(ILogger logger, string source, Guid agentId);

    /// <summary>Logs an optimistic concurrency assignment failure.</summary>
    /// <param name="logger">The logger receiving the diagnostic.</param>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="goalId">The goal identifier.</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Agent {AgentId} lost an assignment race for goal {GoalId}.")]
    private static partial void AssignmentRaceLost(ILogger logger, Guid agentId, Guid goalId);

    /// <summary>Matches non-whitespace prompt tokens.</summary>
    /// <returns>The compiled token-matching expression.</returns>
    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWhitespace();
}
