using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

public class VariantSelectionStateTests
{
    private static ModelVariant V(string id) => new(id, null, null, null);

    [Fact]
    public void Default_effective_is_first_variant()
    {
        var s = new VariantSelectionState(new[] { V("v1"), V("v2") });
        Assert.True(s.HasVariants);
        Assert.Equal("v1", s.EffectiveVariantId);
        Assert.Null(s.PinnedVariantId);
    }

    [Fact]
    public void Pin_is_honored_unknown_is_ignored()
    {
        var s = new VariantSelectionState(new[] { V("v1"), V("v2") });
        s.Pin("v2");
        Assert.Equal("v2", s.EffectiveVariantId);
        s.Pin("nope");
        Assert.Equal("v2", s.EffectiveVariantId); // unchanged, no fabrication
    }

    [Fact]
    public void No_variants_is_honest()
    {
        var s = new VariantSelectionState(System.Array.Empty<ModelVariant>());
        Assert.False(s.HasVariants);
        Assert.Null(s.EffectiveVariantId);
    }
}
