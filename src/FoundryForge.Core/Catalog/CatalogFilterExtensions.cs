using FoundryForge.Core.Models;

namespace FoundryForge.Core.Catalog;

/// <summary>
/// Pure catalog filtering (FR-016, R6). No Foundry Local, no UI — fully unit-testable. Null criteria
/// match everything; text search is case-insensitive over alias/display name/id.
/// </summary>
public static class CatalogFilterExtensions
{
    public static bool Matches(this CatalogFilter filter, ModelInfo model)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(model);

        if (filter.Device is { } device && model.Device != device && !model.Variants.Any(v => v.Device == device))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Task) &&
            !string.Equals(model.Task, filter.Task, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Provider) &&
            !string.Equals(model.Provider, filter.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.CachedOnly && !model.IsCached)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var q = filter.SearchText.Trim();
            var hit =
                model.Alias.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                model.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                model.Id.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!hit)
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<ModelInfo> Apply(this CatalogFilter filter, IEnumerable<ModelInfo> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        return models.Where(filter.Matches).ToList();
    }

    public static IReadOnlyList<ModelInfo> Cached(this IEnumerable<ModelInfo> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        return models.Where(m => m.IsCached).ToList();
    }

    public static IReadOnlyList<ModelInfo> Loaded(this IEnumerable<ModelInfo> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        return models.Where(m => m.IsLoaded).ToList();
    }
}
