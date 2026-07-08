using FoundryForge.Core.Compare;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

/// <summary>
/// Unit tests for <see cref="CompareMetrics"/> — the pure metric accumulator for the Compare workbench.
/// All tests run without a real model; inputs are synthetic (timestamp, token, usage) sequences.
/// </summary>
public class CompareMetricsTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Ttft_is_first_token_minus_send_time()
    {
        var m = new CompareMetrics();
        m.OnSend(T0);
        m.OnToken(T0.AddMilliseconds(300));

        var snap = m.Snapshot(StopReason.Natural);

        Assert.Equal(TimeSpan.FromMilliseconds(300), snap.TimeToFirstToken);
    }

    [Fact]
    public void Tokens_per_second_uses_segment_count_when_no_usage()
    {
        var m = new CompareMetrics();
        m.OnSend(T0);
        m.OnToken(T0.AddMilliseconds(200)); // first token
        m.OnToken(T0.AddMilliseconds(400));
        m.OnToken(T0.AddMilliseconds(600)); // last token — 400 ms window, 3 segments

        var snap = m.Snapshot(StopReason.Natural);

        // 3 segments over (600-200)=400 ms = 7.5 tok/s
        Assert.NotNull(snap.TokensPerSecond);
        Assert.Equal(7.5, snap.TokensPerSecond!.Value, precision: 2);
    }

    [Fact]
    public void Tokens_per_second_prefers_usage_output_count_over_segment_count()
    {
        var m = new CompareMetrics();
        m.OnSend(T0);
        m.OnToken(T0.AddMilliseconds(100));
        m.OnToken(T0.AddMilliseconds(600)); // 500 ms window, 2 segments (would give 4.0)
        m.OnUsage(totalTokens: 50, outputTokens: 10); // engine says 10 output tokens

        var snap = m.Snapshot(StopReason.Natural);

        // 10 tokens over 500 ms = 20 tok/s  (not 4.0)
        Assert.NotNull(snap.TokensPerSecond);
        Assert.Equal(20.0, snap.TokensPerSecond!.Value, precision: 2);
    }

    [Fact]
    public void Total_tokens_comes_from_usage_not_segment_count()
    {
        var m = new CompareMetrics();
        m.OnSend(T0);
        m.OnToken(T0.AddMilliseconds(100));
        m.OnUsage(totalTokens: 42, outputTokens: 30);

        var snap = m.Snapshot(StopReason.Natural);

        Assert.Equal(42, snap.TotalTokens);
    }

    [Fact]
    public void No_usage_means_total_is_honest_null()
    {
        var m = new CompareMetrics();
        m.OnSend(T0);
        m.OnToken(T0.AddMilliseconds(100));

        var snap = m.Snapshot(StopReason.Natural);

        Assert.Null(snap.TotalTokens); // honest unknown, never back-computed
    }

    [Fact]
    public void Ttft_is_null_when_no_token_arrives()
    {
        var m = new CompareMetrics();
        m.OnSend(T0);

        var snap = m.Snapshot(StopReason.UserCancelled);

        Assert.Null(snap.TimeToFirstToken);
        Assert.Equal(StopReason.UserCancelled, snap.StopReason);
    }

    [Fact]
    public void Ttft_is_null_when_send_not_called()
    {
        var m = new CompareMetrics();
        m.OnToken(T0.AddMilliseconds(500));

        var snap = m.Snapshot(StopReason.Natural);

        Assert.Null(snap.TimeToFirstToken);
    }

    [Fact]
    public void Tokens_per_second_is_null_for_single_token()
    {
        // With only one token the elapsed window is 0 s — rate is undefined.
        var m = new CompareMetrics();
        m.OnSend(T0);
        m.OnToken(T0.AddMilliseconds(100)); // first and only token

        var snap = m.Snapshot(StopReason.Natural);

        // elapsed = 0  → tokensPerSecond cannot be computed
        Assert.Null(snap.TokensPerSecond);
    }

    [Fact]
    public void Stop_reason_is_propagated()
    {
        var m = new CompareMetrics();
        var snap = m.Snapshot(StopReason.Error);
        Assert.Equal(StopReason.Error, snap.StopReason);
    }

    [Fact]
    public void Multiple_usage_calls_take_last_values()
    {
        var m = new CompareMetrics();
        m.OnSend(T0);
        m.OnToken(T0.AddMilliseconds(100));
        m.OnUsage(totalTokens: 10, outputTokens: 5);
        m.OnUsage(totalTokens: 55, outputTokens: 40); // terminal frame overrides

        var snap = m.Snapshot(StopReason.Natural);

        Assert.Equal(55, snap.TotalTokens);
    }
}
