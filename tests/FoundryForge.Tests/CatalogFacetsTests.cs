using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

public class CatalogFacetsTests
{
    private static ModelInfo M(string task, string provider, Device? device) =>
        new("a-" + task + provider + device, "id", "disp", null, device, task, provider,
            System.Array.Empty<ModelVariant>(), false, false);

    [Fact]
    public void Derives_distinct_nonempty_facets_excluding_unknowns()
    {
        var models = new[]
        {
            M("chat", "Microsoft", Device.Gpu),
            M("chat", "Microsoft", Device.Cpu),   // dup task/provider
            M("", "Meta", Device.Npu),             // empty task excluded
            M("embed", "", null),                  // empty provider + null device excluded
        };

        var facets = CatalogFacets.Derive(models);

        Assert.Equal(new[] { "chat", "embed" }, facets.Tasks);
        Assert.Equal(new[] { "Meta", "Microsoft" }, facets.Providers);   // sorted, distinct
        Assert.Equal(new[] { Device.Cpu, Device.Gpu, Device.Npu }, facets.Devices);
    }

    [Fact]
    public void Empty_catalog_yields_empty_facets()
    {
        var facets = CatalogFacets.Derive(System.Array.Empty<ModelInfo>());
        Assert.Empty(facets.Tasks);
        Assert.Empty(facets.Providers);
        Assert.Empty(facets.Devices);
    }
}
