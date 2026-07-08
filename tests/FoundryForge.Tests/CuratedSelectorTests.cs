using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

public class CuratedSelectorTests
{
    private static ModelInfo M(string alias) =>
        new(alias, alias + "-id", alias, null, null, "", "", System.Array.Empty<ModelVariant>(), false, false);

    [Fact]
    public void Selects_only_curated_in_allow_list_order()
    {
        var first = CuratedSelector.CuratedAliases[0];
        var second = CuratedSelector.CuratedAliases[1];
        var all = new[] { M("not-curated"), M(second), M(first) };

        var result = CuratedSelector.Select(all);

        Assert.Equal(new[] { first, second }, result.Select(m => m.Alias));
    }

    [Fact]
    public void Missing_curated_alias_is_silently_skipped()
    {
        var first = CuratedSelector.CuratedAliases[0];
        var result = CuratedSelector.Select(new[] { M(first) });
        Assert.Single(result);
        Assert.Equal(first, result[0].Alias);
    }

    [Fact]
    public void Empty_input_yields_empty()
    {
        Assert.Empty(CuratedSelector.Select(System.Array.Empty<ModelInfo>()));
    }
}
