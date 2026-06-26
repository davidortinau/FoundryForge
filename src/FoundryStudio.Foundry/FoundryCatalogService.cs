using FoundryStudio.Core.Abstractions;
using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using ICatalog = Microsoft.AI.Foundry.Local.ICatalog;
using IModel = Microsoft.AI.Foundry.Local.IModel;

namespace FoundryStudio.Foundry;

/// <summary>
/// Wraps Foundry Local catalog/model operations behind the FL-free <see cref="IFoundryCatalogService"/>.
/// Every load/unload/delete routes through the <see cref="IModelStateGate"/> (Constitution V) and cache
/// deletion requires explicit consent (Constitution IV). Rich per-model metadata (size/variants/device)
/// is enriched in M2 — M1 establishes the seam and the gate/consent routing.
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

        var infos = models.Select(m => MapBasic(m, isCached: filter.CachedOnly)).ToList();
        return filter.Apply(infos);
    }

    public async Task<ModelInfo?> GetModelAsync(string alias, CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        var model = await catalog.GetModelAsync(alias, cancellationToken).ConfigureAwait(false);
        return model is null ? null : MapBasic(model);
    }

    public async Task<IReadOnlyList<ModelVariant>> GetVariantsAsync(string alias, CancellationToken cancellationToken = default)
    {
        // TODO(M2): map FL model variants (quant/device) via GetModelVariantAsync. M1 returns the empty seam.
        _ = await GetModelAsync(alias, cancellationToken).ConfigureAwait(false);
        return Array.Empty<ModelVariant>();
    }

    public async Task<IReadOnlyList<ModelInfo>> ListCachedAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        var models = await catalog.GetCachedModelsAsync(cancellationToken).ConfigureAwait(false);
        return models.Select(m => MapBasic(m, isCached: true)).ToList();
    }

    public async Task<IReadOnlyList<ModelInfo>> ListLoadedAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await CatalogAsync(cancellationToken).ConfigureAwait(false);
        var models = await catalog.GetLoadedModelsAsync(cancellationToken).ConfigureAwait(false);
        return models.Select(m => MapBasic(m, isCached: true, isLoaded: true)).ToList();
    }

    public async Task DownloadAsync(string alias, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var model = await ResolveModelAsync(alias, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{alias}' not found in the Foundry Local catalog.");
        await model.DownloadAsync(p => progress?.Report(p), cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(string alias, CancellationToken cancellationToken = default)
    {
        // Routed through the gate: never load while a generation streams on this model.
        await _gate.MutateAsync(alias, MutationPolicy.Drain, async () =>
        {
            var model = await ResolveModelAsync(alias, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Model '{alias}' not found in the Foundry Local catalog.");

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
        if (!userConfirmed)
        {
            throw new InvalidOperationException(
                $"Deleting cached model '{alias}' requires explicit user confirmation (protected user data).");
        }

        // Drain through the gate so we never delete out from under an in-flight generation.
        await _gate.MutateAsync(alias, MutationPolicy.Drain, async () =>
        {
            var model = await ResolveModelAsync(alias, cancellationToken).ConfigureAwait(false);
            if (model is not null && await model.IsCachedAsync(cancellationToken).ConfigureAwait(false))
            {
                await model.RemoveFromCacheAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Strongly-typed model access for the in-process chat adapter (same Foundry project).</summary>
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

    // TODO(M2): enrich Device/Task/Provider/SizeGb/Variants from FL model metadata.
    private static ModelInfo MapBasic(IModel model, bool isCached = false, bool isLoaded = false) => new(
        Alias: model.Alias,
        Id: model.Id,
        DisplayName: model.Alias,
        SizeGb: 0,
        Device: Device.Gpu,
        Task: string.Empty,
        Provider: string.Empty,
        Variants: Array.Empty<ModelVariant>(),
        IsCached: isCached,
        IsLoaded: isLoaded);
}
