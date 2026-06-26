using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using Xunit;

namespace FoundryStudio.Tests;

public class CatalogFilterTests
{
    private static ModelInfo Model(
        string alias = "qwen2.5-0.5b",
        string display = "Qwen 2.5 0.5B",
        string id = "qwen2.5-0.5b-instruct-generic-gpu",
        Device device = Device.Gpu,
        string task = "chat-completion",
        string provider = "Microsoft",
        bool cached = false,
        bool loaded = false) =>
        new(alias, id, display, 0.5, device, task, provider, Array.Empty<ModelVariant>(), cached, loaded);

    [Fact]
    public void Empty_filter_matches_everything()
    {
        var filter = new CatalogFilter();
        Assert.True(filter.Matches(Model()));
    }

    [Fact]
    public void Device_filter_matches_variant_device_too()
    {
        var withCpuVariant = Model(device: Device.Gpu) with
        {
            Variants = new[] { new ModelVariant("cpu-int4", "int4", Device.Cpu, 0.4) },
        };
        Assert.True(new CatalogFilter(Device: Device.Cpu).Matches(withCpuVariant));
        Assert.False(new CatalogFilter(Device: Device.Npu).Matches(Model(device: Device.Gpu)));
    }

    [Fact]
    public void Task_and_provider_filters_are_case_insensitive()
    {
        Assert.True(new CatalogFilter(Task: "CHAT-completion").Matches(Model(task: "chat-completion")));
        Assert.True(new CatalogFilter(Provider: "microsoft").Matches(Model(provider: "Microsoft")));
        Assert.False(new CatalogFilter(Provider: "Meta").Matches(Model(provider: "Microsoft")));
    }

    [Fact]
    public void CachedOnly_excludes_uncached()
    {
        Assert.False(new CatalogFilter(CachedOnly: true).Matches(Model(cached: false)));
        Assert.True(new CatalogFilter(CachedOnly: true).Matches(Model(cached: true)));
    }

    [Fact]
    public void SearchText_matches_alias_display_or_id_case_insensitively()
    {
        Assert.True(new CatalogFilter(SearchText: "QWEN").Matches(Model()));
        Assert.True(new CatalogFilter(SearchText: "0.5B").Matches(Model()));
        Assert.True(new CatalogFilter(SearchText: "generic-gpu").Matches(Model()));
        Assert.False(new CatalogFilter(SearchText: "llama").Matches(Model()));
    }

    [Fact]
    public void Cached_and_Loaded_partition_helpers()
    {
        var models = new[]
        {
            Model(alias: "a", cached: true, loaded: false),
            Model(alias: "b", cached: true, loaded: true),
            Model(alias: "c", cached: false, loaded: false),
        };

        Assert.Equal(2, models.Cached().Count);
        Assert.Single(models.Loaded());
    }
}
