using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RALE.Server.Models;

namespace RALE.Server.Services;

public sealed partial class HttpAgentCapacityClient(HttpClient httpClient, ILogger<HttpAgentCapacityClient> logger) : IAgentCapacityClient
{
    public async Task<AgentCapacity?> QueryCapacityAsync(Agent agent, string taskProfile, CancellationToken cancellationToken = default)
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
            var capacity = ReadInt(root, "capacity", agent.MaxTokenCapacity);
            var maxConcurrentGoals = ReadInt(root, "maxConcurrentGoals", agent.MaxConcurrentGoals);
            var constraints = root.TryGetProperty("constraints", out var constraintsElement)
                ? constraintsElement.GetRawText()
                : "{}";
            var observedAt = DateTimeOffset.UtcNow;
            var ttl = TimeSpan.FromSeconds(Math.Max(1, agent.CapacityCacheTtlSeconds));

            return new AgentCapacity(
                agent.Id,
                Math.Max(1, capacity),
                Math.Max(1, maxConcurrentGoals),
                constraints,
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

    private static int ReadInt(JsonElement root, string propertyName, int fallback)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
            _ => fallback
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Capacity query for agent {AgentId} returned {StatusCode}.")]
    private static partial void CapacityQueryReturnedStatus(ILogger logger, Guid agentId, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Capacity query for agent {AgentId} failed.")]
    private static partial void CapacityQueryFailed(ILogger logger, Exception exception, Guid agentId);
}
