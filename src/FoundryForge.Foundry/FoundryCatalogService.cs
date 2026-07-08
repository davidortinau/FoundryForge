using FoundryForge.Core.Abstractions;
using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;
using ICatalog = Microsoft.AI.Foundry.Local.ICatalog;
using IModel = Microsoft.AI.Foundry.Local.IModel;
using FlDeviceType = Microsoft.AI.Foundry.Local.DeviceType;

namespace FoundryForge.Foundry;

/// <summary>
/// Wraps Foundry Local catalog/model operations behind the FL-free <see cref="IFoundryCatalogService"/>.
/// M2 enriches each model from real FL metadata (<c>IModel.Info</c> + <c>IModel.Variants</c>) into the
/// Core <see cref="ModelInfo"/> DTO with honest nullable fields. Load/unload/delete still route through the
/// <see cref="IModelStateGate"/> (Constitution V) and cache deletion requires consent (Constitution IV) —
/// but M2 is browse-only and triggers none of those.
/// </summary>
public sealed class FoundryCatalogService : IFoundryCatalogService
{
    private readonly FoundryLifecycle _lifecycle;
    private readonly IModelStateGate _gate;

    public FoundryCatalogService(FoundryLifecycle lifecycle, IModelStateGate gate)
    {
        _lifecycle = lifecycle;
        _gate = gate;
    }

    public async Task<IReadOnlyList<ModelInfo>> BrowseAsync(CatalogFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new CatalogFilter();
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);

        var models = filter.CachedOnly
            ? await catalog.GetCachedModelsAsync(cancellationToken).ConfigureAwait(false)
            : await catalog.ListModelsAsync(cancellationToken).ConfigureAwait(false);

        var loadedIds = await LoadedIdsAsync(catalog, cancellationToken).ConfigureAwait(false);
        // KI-009: when sourced from GetCachedModelsAsync the set IS authoritative-cached — mark IsCached
        // true and don't re-apply CachedOnly (which could wrongly drop models if info.Cached is unreliable).
        var effectiveFilter = filter.CachedOnly ? filter with { CachedOnly = false } : filter;
        var infos = models.Select(m => MapEnriched(m, loadedIds.Contains(m.Info.Id), isCachedOverride: filter.CachedOnly ? true : (bool?)null)).ToList();
        return effectiveFilter.Apply(infos);
    }

    public async Task<ModelInfo?> GetModelAsync(string alias, CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        var model = await catalog.GetModelAsync(alias, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return null;
        }

        var loadedIds = await LoadedIdsAsync(catalog, cancellationToken).ConfigureAwait(false);
        return MapEnriched(model, loadedIds.Contains(model.Info.Id));
    }

    public async Task<IReadOnlyList<ModelVariant>> GetVariantsAsync(string alias, CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        var model = await catalog.GetModelAsync(alias, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Array.Empty<ModelVariant>();
        }

        return model.Variants.Select(MapVariant).ToList();
    }

    public async Task<IReadOnlyList<ModelInfo>> ListCachedAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        var loadedIds = await LoadedIdsAsync(catalog, cancellationToken).ConfigureAwait(false);
        var models = await catalog.GetCachedModelsAsync(cancellationToken).ConfigureAwait(false);
        return models.Select(m => MapEnriched(m, loadedIds.Contains(m.Info.Id), isCachedOverride: true)).ToList();
    }

    public async Task<IReadOnlyList<ModelInfo>> ListLoadedAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        var models = await catalog.GetLoadedModelsAsync(cancellationToken).ConfigureAwait(false);
        return models.Select(m => MapEnriched(m, isLoaded: true)).ToList();
    }

    public async Task DownloadAsync(string alias, IProgress<double>? progress = null, string? variantId = null, CancellationToken cancellationToken = default)
    {
        var model = await ResolveTargetAsync(alias, variantId, cancellationToken).ConfigureAwait(false);
        await model.DownloadAsync(p => progress?.Report(p), cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(string alias, string? variantId = null, CancellationToken cancellationToken = default)
    {
        await _gate.MutateAsync(alias, MutationPolicy.Drain, async () =>
        {
            var model = await ResolveTargetAsync(alias, variantId, cancellationToken).ConfigureAwait(false);

            if (!await model.IsCachedAsync(cancellationToken).ConfigureAwait(false))
            {
                await model.DownloadAsync(null, cancellationToken).ConfigureAwait(false);
            }

            if (!await model.IsLoadedAsync(cancellationToken).ConfigureAwait(false))
            {
                await model.LoadAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    // Resolve the model, honoring a pinned variant (FR-020). An unknown variantId is an honest failure, not
    // a silent fallback to the default that would mislead the user.
    private async Task<IModel> ResolveTargetAsync(string alias, string? variantId, CancellationToken cancellationToken)
    {
        var model = await ResolveModelAsync(alias, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{alias}' not found in the Foundry Local catalog.");

        if (variantId is not null)
        {
            var variant = model.Variants.FirstOrDefault(v => string.Equals(v.Info.Id, variantId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Variant '{variantId}' not found for model '{alias}'.");
            model.SelectVariant(variant);
            return variant;
        }

        return model;
    }

    public async Task UnloadAsync(string alias, CancellationToken cancellationToken = default)
    {
        await _gate.MutateAsync(alias, MutationPolicy.Drain, async () =>
        {
            var model = await ResolveModelAsync(alias, cancellationToken).ConfigureAwait(false);
            if (model is not null && await model.IsLoadedAsync(cancellationToken).ConfigureAwait(false))
            {
                await model.UnloadAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteFromCacheAsync(string alias, bool userConfirmed, CancellationToken cancellationToken = default)
    {
        // Constitution IV: the multi-GB model cache is protected user data. No deletion without consent.
        ConsentGuard.RequireConfirmed(userConfirmed, $"Deleting cached model '{alias}'");

        await _gate.MutateAsync(alias, MutationPolicy.Drain, async () =>
        {
            var model = await ResolveModelAsync(alias, cancellationToken).ConfigureAwait(false);
            if (model is not null && await model.IsCachedAsync(cancellationToken).ConfigureAwait(false))
            {
                await model.RemoveFromCacheAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<IModel?> ResolveModelAsync(string alias, CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        return await catalog.GetModelAsync(alias, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ICatalog> CatalogAsync(CancellationToken cancellationToken)
    {
        var manager = await _lifecycle.GetManagerTypedAsync(cancellationToken).ConfigureAwait(false);
        return await manager.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HashSet<string>> LoadedIdsAsync(ICatalog catalog, CancellationToken cancellationToken)
    {
        var loaded = await catalog.GetLoadedModelsAsync(cancellationToken).ConfigureAwait(false);
        return loaded.Select(m => m.Info.Id).ToHashSet(StringComparer.Ordinal);
    }

    // FL IModel.Info (rich metadata) -> Core ModelInfo. Honest nullable mapping: a missing FL field becomes
    // null/empty so the card renders "unknown", never a fabricated value.
    private static ModelInfo MapEnriched(IModel model, bool isLoaded, bool? isCachedOverride = null)
    {
        var info = model.Info;
        var capabilities = CapabilityParser.Parse(info.Capabilities, info.InputModalities, info.SupportsToolCalling);

        return new ModelInfo(
            Alias: info.Alias,
            Id: info.Id,
            DisplayName: string.IsNullOrWhiteSpace(info.DisplayName) ? info.Alias : info.DisplayName!,
            SizeGb: ToGb(info.FileSizeMb),
            Device: MapDevice(info.Runtime?.DeviceType),
            Task: info.Task ?? string.Empty,
            Provider: info.ProviderType ?? string.Empty,
            Variants: model.Variants.Select(MapVariant).ToList(),
            IsCached: isCachedOverride ?? info.Cached,
            IsLoaded: isLoaded,
            ExecutionProvider: info.Runtime?.ExecutionProvider,
            ContextLength: ToInt(info.ContextLength),
            MaxOutputTokens: ToInt(info.MaxOutputTokens),
            License: info.License,
            LicenseDescription: info.LicenseDescription,
            Publisher: info.Publisher,
            ModelType: info.ModelType,
            Capabilities: capabilities);
    }

    private static ModelVariant MapVariant(IModel variant)
    {
        var info = variant.Info;
        return new ModelVariant(
            VariantId: info.Id,
            Quantization: ParseQuantization(info.Id),
            Device: MapDevice(info.Runtime?.DeviceType),
            SizeGb: ToGb(info.FileSizeMb));
    }

    private static double? ToGb(int? fileSizeMb) =>
        fileSizeMb is int mb && mb > 0 ? Math.Round(mb / 1024.0, 2) : null;

    private static int? ToInt(long? value) =>
        value is long v ? (int?)Math.Clamp(v, int.MinValue, int.MaxValue) : null;

    private static Device? MapDevice(FlDeviceType? deviceType) => deviceType switch
    {
        FlDeviceType.CPU => Device.Cpu,
        FlDeviceType.GPU => Device.Gpu,
        FlDeviceType.NPU => Device.Npu,
        _ => null, // Invalid / null -> unknown
    };

    private static string? ParseQuantization(string id)
    {
        string[] tokens = { "int4", "int8", "fp16", "fp32", "q4", "q8", "bf16" };
        foreach (var token in tokens)
        {
            if (id.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return token;
            }
        }

        return null;
    }
}
