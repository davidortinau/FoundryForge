using FoundryForge.Core.Chat;
using Microsoft.Extensions.AI;
using Xunit;

namespace FoundryForge.Tests;

public class InferenceParametersMappingTests
{
    [Fact]
    public void Supported_params_flow_to_matching_chat_options()
    {
        var p = new InferenceParameters(Temperature: 0.5, MaxOutputTokens: 256, TopP: 0.9);

        var o = p.ToChatOptions("qwen2.5-0.5b");

        Assert.Equal("qwen2.5-0.5b", o.ModelId);
        Assert.Equal(0.5f, o.Temperature);
        Assert.Equal(256, o.MaxOutputTokens);
        Assert.Equal(0.9f, o.TopP);
        // FrequencyPenalty is never set: FL corrupts generation when frequency_penalty is present.
        Assert.Null(o.FrequencyPenalty);
    }

    [Fact]
    public void No_unsupported_param_surface_exists()
    {
        // FL ignores top_k/min_p/repeat_penalty/seed and corrupts output on frequency_penalty, so we never
        // surface or set them. InferenceParameters has no such field, and ToChatOptions never populates the
        // MEAI properties that happen to exist on the abstraction (FR-019).
        var ipProps = typeof(InferenceParameters).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("TopK", ipProps);
        Assert.DoesNotContain("MinP", ipProps);
        Assert.DoesNotContain("RepeatPenalty", ipProps);
        Assert.DoesNotContain("Seed", ipProps);
        Assert.DoesNotContain("FrequencyPenalty", ipProps);

        var o = new InferenceParameters(0.5, 256, 0.9).ToChatOptions("m");
        Assert.Null(o.TopK);              // never set by us
        Assert.Null(o.Seed);             // never set by us
        Assert.Null(o.PresencePenalty);  // never set by us
        Assert.Null(o.FrequencyPenalty); // never set by us — FL corrupts output when it is present
    }

    [Fact]
    public void Null_param_leaves_option_unset()
    {
        var p = new InferenceParameters(Temperature: null, MaxOutputTokens: null, TopP: null);

        var o = p.ToChatOptions("m");

        Assert.Null(o.Temperature);
        Assert.Null(o.MaxOutputTokens);
        Assert.Null(o.TopP);
        Assert.Null(o.FrequencyPenalty);
    }

    [Fact]
    public void Tools_attach_when_provided()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "noop");
        var o = InferenceParameters.Defaults.ToChatOptions("m", new[] { tool });

        Assert.NotNull(o.Tools);
        Assert.Single(o.Tools!);
    }

    [Fact]
    public void Empty_tools_leaves_tools_null()
    {
        var o = InferenceParameters.Defaults.ToChatOptions("m", Array.Empty<AITool>());
        Assert.Null(o.Tools);
    }
}
