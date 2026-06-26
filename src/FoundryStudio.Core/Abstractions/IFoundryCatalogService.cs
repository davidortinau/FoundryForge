using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Abstractions;

public interface IFoundryCatalogService
{
    Task<IReadOnlyList<ModelInfo>> BrowseAsync(CatalogFilter? filter = null, CancellationToken cancellationToken = default);

    Task<ModelInfo?> GetModelAsync(string alias, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelVariant>> GetVariantsAsync(string alias, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelInfo>> ListCachedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelInfo>> ListLoadedAsync(CancellationToken cancellationToken = default);

    Task DownloadAsync(string alias, IProgress<double>? progress = null, string? variantId = null, CancellationToken cancellationToken = default);

    Task LoadAsync(string alias, string? variantId = null, CancellationToken cancellationToken = default);

    Task UnloadAsync(string alias, CancellationToken cancellationToken = default);

    Task DeleteFromCacheAsync(string alias, bool userConfirmed, CancellationToken cancellationToken = default);
}
