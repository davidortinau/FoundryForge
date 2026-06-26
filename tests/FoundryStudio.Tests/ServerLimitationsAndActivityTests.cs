using FoundryStudio.Core.Server;
using Xunit;

namespace FoundryStudio.Tests;

public class ServerLimitationsTests
{
    [Fact]
    public void All_states_the_four_real_limits()
    {
        var all = ServerLimitations.All;
        Assert.Equal(4, all.Count);
        Assert.Contains(all, s => s.Contains("Localhost only", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(all, s => s.Contains("No authentication", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(all, s => s.Contains("No LAN", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(all, s => s.Contains("external tools only", System.StringComparison.OrdinalIgnoreCase));
    }
}

public class RequestActivityProjectionTests
{
    [Fact]
    public void Null_observed_is_omitted_with_honest_note()
    {
        var view = RequestActivityProjection.Project(null);
        Assert.False(view.Show);
        Assert.NotNull(view.HonestNote);
        Assert.Empty(view.Entries);
    }

    [Fact]
    public void Empty_observed_is_shown_with_no_entries()
    {
        var view = RequestActivityProjection.Project(System.Array.Empty<RequestActivityEntry>());
        Assert.True(view.Show);
        Assert.Null(view.HonestNote);
        Assert.Empty(view.Entries);
    }

    [Fact]
    public void Real_entries_pass_through_verbatim()
    {
        var e1 = new RequestActivityEntry(System.DateTimeOffset.UnixEpoch, "POST /v1/chat/completions");
        var e2 = new RequestActivityEntry(System.DateTimeOffset.UnixEpoch.AddSeconds(1), "GET /v1/models");
        var view = RequestActivityProjection.Project(new[] { e1, e2 });
        Assert.True(view.Show);
        Assert.Equal(new[] { e1, e2 }, view.Entries);
    }
}
