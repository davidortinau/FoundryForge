using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

public class RamFitHeuristicTests
{
    [Fact]
    public void Plenty_of_free_ram_is_comfortable_but_still_carries_the_caveat()
    {
        var result = RamFitHeuristic.Evaluate(modelSizeGb: 1, freeRamGb: 16);
        Assert.Equal(RamFit.Comfortable, result.Fit);
        Assert.True(result.LongContextCaveat); // never a confident green
    }

    [Fact]
    public void Just_enough_free_ram_is_tight()
    {
        // footprint = 4 * 1.2 = 4.8; free 6 -> margin 1.2 (>=0, < comfortable headroom)
        var result = RamFitHeuristic.Evaluate(modelSizeGb: 4, freeRamGb: 6);
        Assert.Equal(RamFit.Tight, result.Fit);
        Assert.True(result.LongContextCaveat);
    }

    [Fact]
    public void Not_enough_free_ram_is_unlikely_and_drops_the_caveat()
    {
        // footprint = 8 * 1.2 = 9.6; free 6 -> margin negative
        var result = RamFitHeuristic.Evaluate(modelSizeGb: 8, freeRamGb: 6);
        Assert.Equal(RamFit.Unlikely, result.Fit);
        Assert.False(result.LongContextCaveat);
        Assert.True(result.MarginGb < 0);
    }

    [Theory]
    [InlineData(-1, 8)]
    [InlineData(4, -2)]
    public void Negative_inputs_throw(double sizeGb, double freeGb)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RamFitHeuristic.Evaluate(sizeGb, freeGb));
    }
}
