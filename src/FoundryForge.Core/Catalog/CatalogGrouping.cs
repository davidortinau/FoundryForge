using FoundryForge.Core.Models;

namespace FoundryForge.Core.Catalog;

/// <summary>
/// Cached vs available partition (M3, US4). KI-009 resolution: membership is keyed off the AUTHORITATIVE
/// cached alias set (from ListCachedAsync), NOT a re-applied CachedOnly/info.Cached filter — so the cached
/// group is never wrongly empty nor padded. Every model lands in exactly one group; input order preserved.
/// </summary>
public static class CatalogGrouping
{
    public static (IReadOnlyList<ModelInfo> Cached, IReadOnlyList<ModelInfo> Available) Partition(
        IEnumerable<ModelInfo> all, ISet<string> cachedAliases)
    {
        ArgumentNullException.ThrowIfNull(cachedAliases);
        var cached = new List<ModelInfo>();
        var available = new List<ModelInfo>();

        foreach (var model in all ?? Enumerable.Empty<ModelInfo>())
        {
            if (cachedAliases.Contains(model.Alias))
            {
                cached.Add(model);
            }
            else
            {
                available.Add(model);
            }
        }

        return (cached, available);
    }
}
