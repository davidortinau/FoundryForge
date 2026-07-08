using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

public class DiskFitHeuristicTests
{
    [Fact]
    public void Plenty_of_disk_fits()
    {
        var r = DiskFitHeuristic.Evaluate(modelSizeGb: 2, freeDiskGb: 100);
        Assert.Equal(DiskFit.Fits, r.Fit);
        Assert.NotNull(r.MarginGb);
    }

    [Fact]
    public void Too_little_disk_warns_not_blocks()
    {
        var r = DiskFitHeuristic.Evaluate(modelSizeGb: 50, freeDiskGb: 10);
        Assert.Equal(DiskFit.Warn, r.Fit);
        Assert.True(r.MarginGb < 0);
    }

    [Fact]
    public void Unknown_size_is_honest_unknown_not_a_verdict()
    {
        var r = DiskFitHeuristic.Evaluate(modelSizeGb: null, freeDiskGb: 100);
        Assert.Equal(DiskFit.Unknown, r.Fit);
        Assert.Null(r.MarginGb);
    }

    [Fact]
    public void Negative_free_disk_throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => DiskFitHeuristic.Evaluate(2, -1));
    }
}
