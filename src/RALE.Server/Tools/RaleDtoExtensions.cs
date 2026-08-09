// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using RALE.Server.Models;

namespace RALE.Server.Tools;

/// <summary>Maps persisted RALE entities to MCP tool transport objects.</summary>
public static class RaleDtoExtensions
{
    /// <summary>Provides consistent web defaults for persisted JSON arrays.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Provides mappings for agents.</summary>
    /// <param name="agent">The agent to map.</param>
    extension(Agent agent)
    {
        /// <summary>Maps an agent to its transport representation.</summary>
        /// <returns>The mapped agent transport object.</returns>
        public AgentDto ToDto() => MapAgent(agent);
    }

    /// <summary>Provides mappings for goals.</summary>
    /// <param name="goal">The goal to map.</param>
    extension(Goal goal)
    {
        /// <summary>Maps a goal to its transport representation.</summary>
        /// <returns>The mapped goal transport object.</returns>
        public GoalDto ToDto() => MapGoal(goal);
    }

    /// <summary>Provides mappings for goal results.</summary>
    /// <param name="result">The result to map.</param>
    extension(GoalResult result)
    {
        /// <summary>Maps a goal result to its transport representation.</summary>
        /// <returns>The mapped goal-result transport object.</returns>
        public GoalResultDto ToDto() => MapGoalResult(result);
    }

    /// <summary>Provides mappings for loops.</summary>
    /// <param name="loop">The loop to map.</param>
    extension(Loop loop)
    {
        /// <summary>Maps a loop to its transport representation.</summary>
        /// <returns>The mapped loop transport object.</returns>
        public LoopDto ToDto() => MapLoop(loop);
    }

    /// <summary>Provides mappings for agent-capacity observations.</summary>
    /// <param name="capacity">The capacity observation to map.</param>
    extension(Services.AgentCapacity capacity)
    {
        /// <summary>Maps an agent-capacity observation to its transport representation.</summary>
        /// <returns>The mapped capacity transport object.</returns>
        public AgentCapacityDto ToDto() => MapCapacity(capacity);
    }

    /// <summary>Builds an ordered transport representation for a loop.</summary>
    /// <param name="loop">The loop to map.</param>
    /// <returns>The mapped loop.</returns>
    private static LoopDto MapLoop(Loop loop)
    {
        ArgumentNullException.ThrowIfNull(loop);

        var orderedGoals = new Goal[loop.Goals.Count];
        var goalIndex = 0;
        foreach (var goal in loop.Goals)
        {
            orderedGoals[goalIndex] = goal;
            goalIndex++;
        }

        Array.Sort(orderedGoals, static (left, right) => left.Sequence.CompareTo(right.Sequence));

        var goals = new GoalDto[orderedGoals.Length];
        for (var index = 0; index < orderedGoals.Length; index++)
        {
            goals[index] = MapGoal(orderedGoals[index]);
        }

        return new(
            loop.Id,
            loop.PrimaryObjective,
            loop.CreatedAt,
            loop.Status.ToString(),
            loop.TokenLimit,
            loop.ExecutionPattern,
            loop.ConstraintsJson,
            ParseStrings(loop.RequiredArtifactsJson),
            loop.Priority,
            loop.Deadline,
            goals);
    }

    /// <summary>Builds a transport representation for a goal.</summary>
    /// <param name="goal">The goal to map.</param>
    /// <returns>The mapped goal.</returns>
    private static GoalDto MapGoal(Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return new(
            goal.Id,
            goal.LoopId,
            goal.Sequence,
            goal.Description,
            goal.Prompt,
            ParseDependencies(goal.DependsOnJson),
            goal.AssignedAgentId,
            goal.TaskType,
            goal.Priority,
            goal.Deadline,
            ParseStrings(goal.RequiredArtifactsJson),
            goal.ApprovalRequired,
            goal.ApprovalState,
            goal.IterationLimit,
            goal.IterationCount,
            goal.RetryLimit,
            goal.RetryCount,
            goal.PolicyState,
            ParseStrings(goal.PolicyViolationsJson),
            goal.Status.ToString(),
            goal.StartedAt,
            goal.CompletedAt,
            goal.LastHeartbeatAt);
    }

    /// <summary>Builds a transport representation for a goal result.</summary>
    /// <param name="result">The result to map.</param>
    /// <returns>The mapped goal result.</returns>
    private static GoalResultDto MapGoalResult(GoalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(result.Id, result.GoalId, result.Output, result.Metadata, result.CompletedAt);
    }

    /// <summary>Builds a transport representation for a registered agent.</summary>
    /// <param name="agent">The agent to map.</param>
    /// <returns>The mapped agent.</returns>
    private static AgentDto MapAgent(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return new(
            agent.Id,
            agent.Name,
            ParseStrings(agent.Capabilities),
            agent.Endpoint,
            agent.MaxConcurrentGoals,
            agent.MaxTokenCapacity,
            ParseStrings(agent.SupportedTaskTypesJson),
            agent.Sla,
            agent.SecurityPosture,
            agent.TrustLevel,
            agent.CurrentLoad,
            ParseStrings(agent.ToolScopesJson),
            agent.CapacityCacheTtlSeconds,
            agent.CachedCapacity,
            agent.CachedCapacityConstraintsJson,
            agent.CapacityCheckedAt,
            agent.CapacityExpiresAt,
            agent.AssignedGoalId);
    }

    /// <summary>Builds a transport representation for an agent-capacity observation.</summary>
    /// <param name="capacity">The capacity observation to map.</param>
    /// <returns>The mapped capacity observation.</returns>
    private static AgentCapacityDto MapCapacity(Services.AgentCapacity capacity)
    {
        ArgumentNullException.ThrowIfNull(capacity);

        return new(
            capacity.AgentId,
            capacity.Capacity,
            capacity.MaxConcurrentGoals,
            capacity.ConstraintsJson,
            capacity.ObservedAt,
            capacity.ExpiresAt,
            capacity.Source);
    }

    /// <summary>Parses persisted goal dependency identifiers.</summary>
    /// <param name="value">The persisted JSON value.</param>
    /// <returns>The parsed dependency identifiers.</returns>
    private static Guid[] ParseDependencies(string value) => string.IsNullOrWhiteSpace(value)
        ? []
        : JsonSerializer.Deserialize<Guid[]>(value, JsonOptions) ?? [];

    /// <summary>Parses a persisted string array.</summary>
    /// <param name="value">The persisted JSON value.</param>
    /// <returns>The parsed strings.</returns>
    private static string[] ParseStrings(string value) => string.IsNullOrWhiteSpace(value)
        ? []
        : JsonSerializer.Deserialize<string[]>(value, JsonOptions) ?? [];
}
