using System.Text.Json;
using RALE.Server.Models;

namespace RALE.Server.Tools;

public sealed record LoopDto(
    Guid Id,
    string PrimaryObjective,
    DateTimeOffset CreatedAt,
    string Status,
    int TokenLimit,
    IReadOnlyList<GoalDto> Goals);

public sealed record GoalDto(
    Guid Id,
    Guid LoopId,
    int Sequence,
    string Description,
    string Prompt,
    IReadOnlyList<Guid> DependsOn,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record GoalResultDto(Guid Id, Guid GoalId, string Output, string Metadata, DateTimeOffset CompletedAt);

public static class RaleDtoMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LoopDto ToDto(this Loop loop) =>
        loop is null
            ? throw new ArgumentNullException(nameof(loop))
            : new(
            loop.Id,
            loop.PrimaryObjective,
            loop.CreatedAt,
            loop.Status.ToString(),
            loop.TokenLimit,
            loop.Goals.OrderBy(goal => goal.Sequence).Select(ToDto).ToArray());

    public static GoalDto ToDto(this Goal goal) =>
        goal is null
            ? throw new ArgumentNullException(nameof(goal))
            : new(
            goal.Id,
            goal.LoopId,
            goal.Sequence,
            goal.Description,
            goal.Prompt,
            ParseDependencies(goal.DependsOnJson),
            goal.Status.ToString(),
            goal.StartedAt,
            goal.CompletedAt);

    public static GoalResultDto ToDto(this GoalResult result) =>
        result is null
            ? throw new ArgumentNullException(nameof(result))
            : new(result.Id, result.GoalId, result.Output, result.Metadata, result.CompletedAt);

    private static IReadOnlyList<Guid> ParseDependencies(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Guid[]>(value, JsonOptions) ?? [];
    }
}
