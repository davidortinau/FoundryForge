using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using Xunit;

namespace FoundryStudio.Tests;

public class CatalogGroupingTests
{
    private static ModelInfo M(string alias, bool isCachedFlag) =>
        new(alias, alias + "-id", alias, null, null, "", "", System.Array.Empty<ModelVariant>(), isCachedFlag, false);

    [Fact]
    public void Partitions_by_authoritative_cached_set_not_the_info_flag()
    {
        // KI-009 proof: a model whose IsCached flag disagrees with the cached set is grouped by the SET.
        var models = new[] { M("a", isCachedFlag: false), M("b", isCachedFlag: true), M("c", isCachedFlag: false) };
        var cachedAliases = new System.Collections.Generic.HashSet<string> { "a" }; // 'a' cached per set, flag says false

        var (cached, available) = CatalogGrouping.Partition(models, cachedAliases);

        Assert.Equal(new[] { "a" }, cached.Select(m => m.Alias));
        Assert.Equal(new[] { "b", "c" }, available.Select(m => m.Alias)); // 'b' flag true but not in set -> available
        Assert.Equal(3, cached.Count + available.Count); // every model in exactly one group
    }

    [Fact]
    public void Empty_cached_set_makes_all_available()
    {
        var models = new[] { M("a", true), M("b", true) };
        var (cached, available) = CatalogGrouping.Partition(models, new System.Collections.Generic.HashSet<string>());
        Assert.Empty(cached);
        Assert.Equal(2, available.Count);
    }
}
