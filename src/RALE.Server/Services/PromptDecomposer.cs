using System.Text.RegularExpressions;
using RALE.Server.Models;

namespace RALE.Server.Services;

public sealed partial class PromptDecomposer
{
    public static IReadOnlyList<GoalDraft> Decompose(string primaryPrompt, int tokenLimit)
    {
        Validate(primaryPrompt, tokenLimit);

        var normalized = Whitespace().Replace(primaryPrompt.Trim(), " ");
        var chunks = SplitIntoChunks(normalized, tokenLimit);

        return chunks
            .Select((prompt, index) => new GoalDraft(
                index + 1,
                CreateDescription(index + 1, prompt),
                prompt))
            .ToArray();
    }

    public static int EstimateTokens(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Math.Max(1, (int)Math.Ceiling(value.Length / 4d));
    }

    private static void Validate(string primaryPrompt, int tokenLimit)
    {
        if (string.IsNullOrWhiteSpace(primaryPrompt))
        {
            throw new ArgumentException("A primary prompt is required.", nameof(primaryPrompt));
        }

        if (tokenLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenLimit), tokenLimit, "Token limit must be greater than zero.");
        }
    }

    private static IReadOnlyList<string> SplitIntoChunks(string prompt, int limit)
    {
        if (prompt.Length <= limit)
        {
            return [prompt];
        }

        var chunks = new List<string>();
        var current = string.Empty;

        foreach (Match match in NonWhitespace().Matches(prompt))
        {
            var word = match.Value;

            if (word.Length > limit)
            {
                AddCurrent(chunks, ref current);
                SplitLongToken(chunks, word, limit);
                continue;
            }

            var candidateLength = current.Length == 0 ? word.Length : current.Length + 1 + word.Length;
            if (candidateLength > limit)
            {
                AddCurrent(chunks, ref current);
                current = word;
            }
            else
            {
                current = current.Length == 0 ? word : $"{current} {word}";
            }
        }

        AddCurrent(chunks, ref current);
        return chunks;
    }

    private static void SplitLongToken(List<string> chunks, string token, int limit)
    {
        for (var offset = 0; offset < token.Length; offset += limit)
        {
            chunks.Add(token.Substring(offset, Math.Min(limit, token.Length - offset)));
        }
    }

    private static void AddCurrent(List<string> chunks, ref string current)
    {
        if (current.Length == 0)
        {
            return;
        }

        chunks.Add(current);
        current = string.Empty;
    }

    private static string CreateDescription(int sequence, string prompt)
    {
        var preview = prompt.Length <= 80 ? prompt : string.Concat(prompt.AsSpan(0, 77), "...");
        return $"Goal {sequence}: {preview}";
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWhitespace();
}
