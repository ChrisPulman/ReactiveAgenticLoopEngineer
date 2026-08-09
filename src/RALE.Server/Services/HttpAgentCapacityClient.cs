// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Queries an agent capacity endpoint over HTTP.</summary>
/// <param name="httpClient">The HTTP client used to query agent endpoints.</param>
/// <param name="timeProvider">The source of capacity observation timestamps.</param>
/// <param name="logger">The logger used for capacity-query diagnostics.</param>
public sealed partial class HttpAgentCapacityClient(HttpClient httpClient, TimeProvider timeProvider, ILogger<HttpAgentCapacityClient> logger) : IAgentCapacityClient
{
    /// <inheritdoc />
    public async Task<AgentCapacity?> QueryCapacityAsync(Agent agent, string taskProfile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrWhiteSpace(agent.Endpoint))
        {
            return null;
        }

        var endpoint = agent.Endpoint.TrimEnd('/');
        var requestUri = new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"{endpoint}/agents/{Uri.EscapeDataString(agent.Id.ToString())}/capacity?taskProfile={Uri.EscapeDataString(taskProfile)}"));

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                CapacityQueryReturnedStatus(logger, agent.Id, response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var availableCapacity = ReadInt(root, "capacity", agent.MaxTokenCapacity);
            var concurrentGoalLimit = ReadInt(root, "maxConcurrentGoals", agent.MaxConcurrentGoals);
            var constraintsJson = root.TryGetProperty("constraints", out var constraintsElement)
                ? constraintsElement.GetRawText()
                : "{}";
            var observedAt = timeProvider.GetUtcNow();
            var ttl = TimeSpan.FromSeconds(Math.Max(1, agent.CapacityCacheTtlSeconds));

            return new(
                agent.Id,
                Math.Max(1, availableCapacity),
                Math.Max(1, concurrentGoalLimit),
                constraintsJson,
                observedAt,
                observedAt.Add(ttl),
                "live");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            CapacityQueryFailed(logger, ex, agent.Id);
            return null;
        }
    }

    /// <summary>Reads an integer JSON property or returns a fallback value.</summary>
    /// <param name="root">The root JSON value to inspect.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="fallback">The value to return when the property is absent or invalid.</param>
    /// <returns>The parsed integer value, or <paramref name="fallback"/>.</returns>
    private static int ReadInt(JsonElement root, string propertyName, int fallback) => root.TryGetProperty(propertyName, out var property)
        ? property.ValueKind switch
        {
            JsonValueKind.Number => ReadNumber(property, fallback),
            JsonValueKind.String => ReadString(property, fallback),
            _ => fallback
        }
        : fallback;

    /// <summary>Reads an integer-valued JSON number.</summary>
    /// <param name="property">The JSON number property.</param>
    /// <param name="fallback">The value to return when parsing fails.</param>
    /// <returns>The parsed integer value, or <paramref name="fallback"/>.</returns>
    private static int ReadNumber(JsonElement property, int fallback) => property.TryGetInt32(out var value) ? value : fallback;

    /// <summary>Reads an integer-valued JSON string.</summary>
    /// <param name="property">The JSON string property.</param>
    /// <param name="fallback">The value to return when parsing fails.</param>
    /// <returns>The parsed integer value, or <paramref name="fallback"/>.</returns>
    private static int ReadString(JsonElement property, int fallback) => int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Capacity query for agent {AgentId} returned {StatusCode}.")]
    private static partial void CapacityQueryReturnedStatus(ILogger logger, Guid agentId, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Capacity query for agent {AgentId} failed.")]
    private static partial void CapacityQueryFailed(ILogger logger, Exception exception, Guid agentId);
}
