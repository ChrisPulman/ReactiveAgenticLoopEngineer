using RALE.Server.Services;

namespace RALE.Tests;

public sealed class PromptDecomposerTests
{
    [Test]
    public async Task Decompose_never_emits_prompt_over_limit()
    {
        var drafts = PromptDecomposer.Decompose("alpha beta gamma delta epsilon zeta eta theta iota kappa lambda", 18);

        await Assert.That(drafts.Count > 1).IsTrue();
        await Assert.That(drafts.All(draft => draft.Prompt.Length <= 18)).IsTrue();
    }

    [Test]
    public async Task Decompose_splits_single_long_token()
    {
        var drafts = PromptDecomposer.Decompose("abcdefghijklmnopqrstuvwxyz", 5);

        await Assert.That(drafts.Count).IsEqualTo(6);
        await Assert.That(drafts.All(draft => draft.Prompt.Length <= 5)).IsTrue();
    }
}
