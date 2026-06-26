using FoundryStudio.Core.Chat;
using FoundryStudio.Core.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace FoundryStudio.Tests;

public class TokenStatsAccumulatorTests
{
    private static ChatResponseUpdate Text(string t) => new(ChatRole.Assistant, t);

    [Fact]
    public void Ttft_and_rate_computed_from_real_timing()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var acc = new TokenStatsAccumulator();
        acc.OnSend(t0);
        acc.OnUpdate(Text("a"), t0.AddMilliseconds(200)); // first token at +200ms
        acc.OnUpdate(Text("b"), t0.AddMilliseconds(400));
        acc.OnUpdate(Text("c"), t0.AddMilliseconds(600));

        var m = acc.Complete(StopReason.Natural);

        Assert.Equal(TimeSpan.FromMilliseconds(200), m.TimeToFirstToken);
        // 3 segments over (600-200)=400ms => 7.5 tok/sec
        Assert.NotNull(m.TokensPerSecond);
        Assert.True(m.TokensPerSecond > 0);
        Assert.Equal(StopReason.Natural, m.StopReason);
    }

    [Fact]
    public void Usage_content_yields_real_total()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var acc = new TokenStatsAccumulator();
        acc.OnSend(t0);
        acc.OnUpdate(Text("hello"), t0.AddMilliseconds(100));

        var usageUpdate = new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = new List<AIContent> { new UsageContent(new UsageDetails { TotalTokenCount = 42, OutputTokenCount = 30 }) }
        };
        acc.OnUpdate(usageUpdate, t0.AddMilliseconds(200));

        var m = acc.Complete(StopReason.Natural);
        Assert.Equal(42, m.TotalTokens);
    }

    [Fact]
    public void No_usage_means_total_is_unknown_null()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var acc = new TokenStatsAccumulator();
        acc.OnSend(t0);
        acc.OnUpdate(Text("hi"), t0.AddMilliseconds(50));

        var m = acc.Complete(StopReason.Natural);
        Assert.Null(m.TotalTokens); // honest unknown, never back-computed
    }

    [Fact]
    public void Cancelled_stop_reason_honored()
    {
        var acc = new TokenStatsAccumulator();
        acc.OnSend(DateTimeOffset.UnixEpoch);
        var m = acc.Complete(StopReason.UserCancelled);
        Assert.Equal(StopReason.UserCancelled, m.StopReason);
        Assert.Null(m.TimeToFirstToken); // no token ever arrived
    }
}
