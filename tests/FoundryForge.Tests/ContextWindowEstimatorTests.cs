using FoundryForge.Core.Chat;
using Xunit;

namespace FoundryForge.Tests;

public class ContextWindowEstimatorTests
{
    [Fact]
    public void Fraction_advances_with_usage()
    {
        var e = ContextWindowEstimator.Estimate(usedTokensEstimate: 1000, contextLength: 4000);
        Assert.False(e.IsUnknown);
        Assert.Equal(0.25, e.Fraction);
        Assert.False(e.IsWarn);
    }

    [Fact]
    public void Crossing_warn_fraction_warns()
    {
        var e = ContextWindowEstimator.Estimate(usedTokensEstimate: 3400, contextLength: 4000, warnFraction: 0.8);
        Assert.True(e.IsWarn);
        Assert.Equal(0.85, e.Fraction!.Value, 3);
    }

    [Fact]
    public void Unknown_context_length_fabricates_nothing()
    {
        var e = ContextWindowEstimator.Estimate(usedTokensEstimate: 1000, contextLength: null);
        Assert.True(e.IsUnknown);
        Assert.Null(e.Fraction);
        Assert.False(e.IsWarn);
    }

    [Fact]
    public void Zero_usage_is_fraction_zero()
    {
        var e = ContextWindowEstimator.Estimate(0, 4000);
        Assert.Equal(0.0, e.Fraction);
    }

    [Fact]
    public void Approximate_tokens_is_labeled_estimate_heuristic()
    {
        Assert.Equal(0, ContextWindowEstimator.ApproximateTokens(""));
        Assert.Equal(1, ContextWindowEstimator.ApproximateTokens("abcd"));   // 4 chars ~ 1 token
        Assert.Equal(2, ContextWindowEstimator.ApproximateTokens("abcde"));  // 5 chars -> ceil(1.25)
    }
}
