using FoundryStudio.Core.Models;
using FoundryStudio.Core.Personalization;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// Tests for <see cref="ModelRanker"/>.
/// PURE — no IO, no DI. All inputs are inline fixtures.
/// </summary>
public class ModelRankerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ModelInfo MakeModel(
        string alias,
        bool toolCalling = false,
        bool reasoning = false,
        bool vision = false,
        string task = "chat-completion",
        double? sizeGb = null) =>
        new(
            Alias: alias,
            Id: alias,
            DisplayName: alias,
            SizeGb: sizeGb,
            Device: null,
            Task: task,
            Provider: "Test",
            Variants: Array.Empty<ModelVariant>(),
            IsCached: false,
            IsLoaded: false,
            Capabilities: new ModelCapabilities(
                Vision: vision,
                ToolCalling: toolCalling,
                Reasoning: reasoning,
                ToolCallingKnown: true));

    // ── Null / empty guards ───────────────────────────────────────────────────

    [Fact]
    public void Null_profile_returns_original_order()
    {
        var models = new[] { MakeModel("a"), MakeModel("b"), MakeModel("c") };
        var result = ModelRanker.Rank(null, models);
        Assert.Equal(models, result);
    }

    [Fact]
    public void Empty_profile_returns_original_order()
    {
        var models = new[] { MakeModel("a"), MakeModel("b"), MakeModel("c") };
        var result = ModelRanker.Rank(ContextProfile.Empty, models);
        Assert.Equal(models, result);
    }

    [Fact]
    public void Empty_model_list_returns_empty_list()
    {
        var profile = LocalContextProfiler.Derive("agent agentic", null);
        var result = ModelRanker.Rank(profile, Array.Empty<ModelInfo>());
        Assert.Empty(result);
    }

    // ── Capability-based boosting ─────────────────────────────────────────────

    [Fact]
    public void Agentic_profile_boosts_tool_calling_model_to_top()
    {
        var toolModel = MakeModel("tool-model", toolCalling: true);
        var plainModel = MakeModel("plain-model", toolCalling: false);
        var models = new[] { plainModel, toolModel };

        var profile = LocalContextProfiler.Derive("agent agentic skill automation", null);
        var result = ModelRanker.Rank(profile, models);

        Assert.Equal("tool-model", result[0].Alias);
    }

    [Fact]
    public void Reasoning_profile_boosts_reasoning_model_to_top()
    {
        var reasonModel = MakeModel("reason-model", reasoning: true);
        var plainModel = MakeModel("plain-model", reasoning: false);
        var models = new[] { plainModel, reasonModel };

        var profile = LocalContextProfiler.Derive("reasoning analyze structured thinking", null);
        var result = ModelRanker.Rank(profile, models);

        Assert.Equal("reason-model", result[0].Alias);
    }

    [Fact]
    public void Vision_profile_boosts_vision_model_to_top()
    {
        var visionModel = MakeModel("vision-model", vision: true);
        var plainModel = MakeModel("plain-model", vision: false);
        var models = new[] { plainModel, visionModel };

        var profile = LocalContextProfiler.Derive("vision image screenshot visual", null);
        var result = ModelRanker.Rank(profile, models);

        Assert.Equal("vision-model", result[0].Alias);
    }

    [Fact]
    public void All_models_are_present_in_result_none_are_removed()
    {
        var models = new[]
        {
            MakeModel("a"),
            MakeModel("b", toolCalling: true),
            MakeModel("c", reasoning: true),
            MakeModel("d"),
        };

        var profile = LocalContextProfiler.Derive("agent agentic reasoning", null);
        var result = ModelRanker.Rank(profile, models);

        Assert.Equal(models.Length, result.Count);
        Assert.All(models, m => Assert.Contains(result, r => r.Alias == m.Alias));
    }

    // ── Task-based boosting ───────────────────────────────────────────────────

    [Fact]
    public void Coding_profile_boosts_code_task_model()
    {
        var codeModel = MakeModel("code-model", task: "code-generation");
        var chatModel = MakeModel("chat-model", task: "image-generation");
        var models = new[] { chatModel, codeModel };

        var profile = LocalContextProfiler.Derive("coding software development build", null);
        var result = ModelRanker.Rank(profile, models);

        Assert.Equal("code-model", result[0].Alias);
    }

    // ── Stable sort (original order preserved for equal scores) ──────────────

    [Fact]
    public void Models_with_equal_score_preserve_original_relative_order()
    {
        // None have tool-calling; original order a, b, c should be preserved
        var models = new[] { MakeModel("a"), MakeModel("b"), MakeModel("c") };

        var profile = new ContextProfile(
            [new ProfileSignal(SignalDomains.Agentic, "agentic / tools", 1.0f, "test")]);

        var result = ModelRanker.Rank(profile, models);

        // All have score 0 (no tool-calling) → original order maintained
        Assert.Equal("a", result[0].Alias);
        Assert.Equal("b", result[1].Alias);
        Assert.Equal("c", result[2].Alias);
    }

    [Fact]
    public void Multiple_matching_capabilities_accumulate_higher_score()
    {
        var superModel = MakeModel("super", toolCalling: true, reasoning: true, vision: true);
        var reasonModel = MakeModel("reason", reasoning: true);
        var plainModel = MakeModel("plain");

        var models = new[] { plainModel, reasonModel, superModel };

        var profile = LocalContextProfiler.Derive(
            "agent agentic reasoning analyze image vision", null);

        var result = ModelRanker.Rank(profile, models);

        Assert.Equal("super", result[0].Alias);
    }

    // ── ComputeScore unit tests ───────────────────────────────────────────────

    [Fact]
    public void ComputeScore_returns_zero_for_no_matching_signals()
    {
        var profile = new ContextProfile(
            [new ProfileSignal(SignalDomains.Vision, "vision", 1.0f, "test")]);
        var model = MakeModel("plain", vision: false);

        var score = ModelRanker.ComputeScore(profile, model);
        Assert.Equal(0f, score);
    }

    [Fact]
    public void ComputeScore_returns_positive_for_matching_capability()
    {
        var profile = new ContextProfile(
            [new ProfileSignal(SignalDomains.Agentic, "agentic / tools", 1.0f, "test")]);
        var model = MakeModel("tool", toolCalling: true);

        var score = ModelRanker.ComputeScore(profile, model);
        Assert.True(score > 0f);
    }

    [Fact]
    public void ComputeScore_gives_higher_score_for_higher_weight_signal()
    {
        var highProfile = new ContextProfile(
            [new ProfileSignal(SignalDomains.Agentic, "agentic / tools", 1.0f, "test")]);
        var lowProfile = new ContextProfile(
            [new ProfileSignal(SignalDomains.Agentic, "agentic / tools", 0.3f, "test")]);
        var model = MakeModel("tool", toolCalling: true);

        var highScore = ModelRanker.ComputeScore(highProfile, model);
        var lowScore = ModelRanker.ComputeScore(lowProfile, model);

        Assert.True(highScore > lowScore);
    }

    // ── Mobile/dotnet small-model preference ──────────────────────────────────

    [Fact]
    public void Mobile_profile_slightly_boosts_small_models()
    {
        var smallModel = MakeModel("small", sizeGb: 1.5);
        var largeModel = MakeModel("large", sizeGb: 14.0);
        var models = new[] { largeModel, smallModel };

        var profile = LocalContextProfiler.Derive("maui mobile ios android", null);
        var result = ModelRanker.Rank(profile, models);

        // Small model gets a boost from mobile signal (size < 5 GB)
        Assert.Equal("small", result[0].Alias);
    }

    [Fact]
    public void Mobile_profile_does_not_boost_model_with_no_size_info()
    {
        // sizeGb = null → no boost; sizeGb = 1.5 → gets boost
        var profile = new ContextProfile(
            [new ProfileSignal(SignalDomains.Mobile, "mobile", 1.0f, "test")]);

        var noSizeModel = MakeModel("no-size", sizeGb: null);
        var smallModel = MakeModel("small", sizeGb: 1.5);

        var noSizeScore = ModelRanker.ComputeScore(profile, noSizeModel);
        var smallScore = ModelRanker.ComputeScore(profile, smallModel);

        Assert.Equal(0f, noSizeScore);
        Assert.True(smallScore > 0f);
    }

    // ── Result count matches input count ──────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public void Result_count_always_matches_input_count(int count)
    {
        var models = Enumerable.Range(0, count)
            .Select(i => MakeModel($"model-{i}"))
            .ToList();

        var profile = LocalContextProfiler.Derive("agent vision reasoning", null);
        var result = ModelRanker.Rank(profile, models);

        Assert.Equal(count, result.Count);
    }
}
