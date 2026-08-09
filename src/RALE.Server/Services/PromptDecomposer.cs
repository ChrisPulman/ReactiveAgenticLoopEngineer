// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using RALE.Server.Models;

namespace RALE.Server.Services;

/// <summary>Splits primary prompts into ordered, bounded goal drafts.</summary>
public static partial class PromptDecomposer
{
    /// <summary>Defines the estimated number of characters represented by one token.</summary>
    private const double EstimatedCharactersPerToken = 4D;

    /// <summary>Defines the maximum length of a goal description preview.</summary>
    private const int DescriptionMaxLength = 80;

    /// <summary>Defines the length of the truncation ellipsis.</summary>
    private const int DescriptionEllipsisLength = 3;

    /// <summary>Defines the maximum preview length before appending an ellipsis.</summary>
    private const int DescriptionPrefixLength = DescriptionMaxLength - DescriptionEllipsisLength;

    /// <summary>Splits a prompt into ordered, bounded goal drafts.</summary>
    /// <param name="primaryPrompt">The prompt to split.</param>
    /// <param name="tokenLimit">The maximum character budget for each draft.</param>
    /// <returns>The ordered goal drafts.</returns>
    public static IReadOnlyList<GoalDraft> Decompose(string primaryPrompt, int tokenLimit)
    {
        Validate(primaryPrompt, tokenLimit);

        var normalized = Whitespace().Replace(primaryPrompt.Trim(), " ");
        var chunks = SplitIntoChunks(normalized, tokenLimit);

        var drafts = new GoalDraft[chunks.Count];
        for (var index = 0; index < chunks.Count; index++)
        {
            var sequence = index + 1;
            var prompt = chunks[index];
            drafts[index] = new(sequence, CreateDescription(sequence, prompt), prompt);
        }

        return drafts;
    }

    /// <summary>Estimates the number of tokens represented by a string.</summary>
    /// <param name="value">The value to estimate.</param>
    /// <returns>A non-zero token estimate.</returns>
    public static int EstimateTokens(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Math.Max(1, (int)Math.Ceiling(value.Length / EstimatedCharactersPerToken));
    }

    /// <summary>Validates decomposition inputs.</summary>
    /// <param name="primaryPrompt">The primary prompt.</param>
    /// <param name="tokenLimit">The token limit.</param>
    private static void Validate(string primaryPrompt, int tokenLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryPrompt);

        if (tokenLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenLimit), tokenLimit, "Token limit must be greater than zero.");
        }
    }

    /// <summary>Splits normalized prompt text at word boundaries.</summary>
    /// <param name="prompt">The normalized prompt.</param>
    /// <param name="limit">The chunk size limit.</param>
    /// <returns>The chunks.</returns>
    private static List<string> SplitIntoChunks(string prompt, int limit)
    {
        if (prompt.Length <= limit)
        {
            return [prompt];
        }

        var chunks = new List<string>();
        var current = string.Empty;

        var matches = NonWhitespace().Matches(prompt);
        for (var matchIndex = 0; matchIndex < matches.Count; matchIndex++)
        {
            var match = matches[matchIndex];
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

    /// <summary>Splits a token that exceeds the configured limit.</summary>
    /// <param name="chunks">The output chunks.</param>
    /// <param name="token">The oversized token.</param>
    /// <param name="limit">The chunk size limit.</param>
    private static void SplitLongToken(List<string> chunks, string token, int limit)
    {
        for (var offset = 0; offset < token.Length; offset += limit)
        {
            chunks.Add(token.Substring(offset, Math.Min(limit, token.Length - offset)));
        }
    }

    /// <summary>Adds the pending chunk when it has content.</summary>
    /// <param name="chunks">The output chunks.</param>
    /// <param name="current">The pending chunk.</param>
    private static void AddCurrent(List<string> chunks, ref string current)
    {
        if (current.Length == 0)
        {
            return;
        }

        chunks.Add(current);
        current = string.Empty;
    }

    /// <summary>Creates a concise description for a goal draft.</summary>
    /// <param name="sequence">The goal sequence.</param>
    /// <param name="prompt">The goal prompt.</param>
    /// <returns>The description.</returns>
    private static string CreateDescription(int sequence, string prompt)
    {
        var preview = prompt.Length <= DescriptionMaxLength
            ? prompt
            : string.Concat(prompt.AsSpan(0, DescriptionPrefixLength), "...");
        return $"Goal {sequence}: {preview}";
    }

    /// <summary>Matches runs of whitespace.</summary>
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>Matches non-whitespace tokens.</summary>
    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWhitespace();
}
