// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using RALE.Server.Services;

namespace RALE.Tests;

/// <summary>Verifies prompt decomposition respects the supplied character budget.</summary>
public sealed class PromptDecomposerTests
{
    /// <summary>Defines the character limit for the multi-word prompt scenario.</summary>
    private const int LongPromptCharacterLimit = 18;

    /// <summary>Defines the character limit for the single-token prompt scenario.</summary>
    private const int LongTokenCharacterLimit = 5;

    /// <summary>Defines the expected number of chunks for the single-token prompt scenario.</summary>
    private const int ExpectedLongTokenChunkCount = 6;

    /// <summary>Ensures every generated draft is within the configured character limit.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task Decompose_never_emits_prompt_over_limit()
    {
        var drafts = PromptDecomposer.Decompose("alpha beta gamma delta epsilon zeta eta theta iota kappa lambda", LongPromptCharacterLimit);

        await Assert.That(drafts.Count > 1).IsTrue();
        foreach (var draft in drafts)
        {
            await Assert.That(draft.Prompt.Length <= LongPromptCharacterLimit).IsTrue();
        }
    }

    /// <summary>Ensures a single token exceeding the character limit is split into compliant drafts.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task Decompose_splits_single_long_token()
    {
        var drafts = PromptDecomposer.Decompose("abcdefghijklmnopqrstuvwxyz", LongTokenCharacterLimit);

        await Assert.That(drafts.Count).IsEqualTo(ExpectedLongTokenChunkCount);
        foreach (var draft in drafts)
        {
            await Assert.That(draft.Prompt.Length <= LongTokenCharacterLimit).IsTrue();
        }
    }
}
