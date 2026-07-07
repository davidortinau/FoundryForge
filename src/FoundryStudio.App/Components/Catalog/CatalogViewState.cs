using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using FoundryStudio.Core.Personalization;
using Device = FoundryStudio.Core.Models.Device;

namespace FoundryStudio.App.Components.Catalog;

// NL-assist state is stored here so the page component stays focused on rendering.

public sealed class CatalogViewState
{
    public enum CatalogStatus
    {
        Loading,
        Populated,
        Empty,
        Error,
    }

    public enum CatalogViewMode
    {
        Curated,
        Full,
    }

    public CatalogStatus Status { get; set; } = CatalogStatus.Loading;

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<ModelInfo> AllModels { get; set; } = Array.Empty<ModelInfo>();

    public CatalogFilter Filter { get; set; } = new();

    public CatalogViewMode ViewMode { get; set; } = CatalogViewMode.Curated;

    // ── NL-assist state ────────────────────────────────────────────────────────
    /// <summary>Current text in the NL input box (not yet interpreted).</summary>
    public string NlInputText { get; set; } = string.Empty;

    /// <summary>The last interpreted NL result; null if the box is empty or cleared.</summary>
    public NlQueryResult? NlResult { get; set; }

    /// <summary>Sort hint produced by the last NL interpretation; applied in Recompute.</summary>
    public NlSortHint NlSortHint { get; set; } = NlSortHint.None;

    // ── Personalization state ──────────────────────────────────────────────────
    /// <summary>
    /// The active context profile. <see cref="ContextProfile.Empty"/> when personalization
    /// is disabled or context could not be read. Set externally by the page component after
    /// reading on-device context with the user's consent.
    /// </summary>
    public ContextProfile PersonalizationProfile { get; set; } = ContextProfile.Empty;

    /// <summary>
    /// True when the personalized boost ordering should be applied in <see cref="Recompute"/>.
    /// Mirrors the <c>PersonalizedRecommendations</c> setting; the page component sets this
    /// after loading settings so the state object stays pure.
    /// </summary>
    public bool PersonalizationActive { get; set; } = false;

    public IReadOnlyList<ModelInfo> Visible { get; private set; } = Array.Empty<ModelInfo>();

    public IReadOnlyList<ModelInfo> VisibleCached { get; private set; } = Array.Empty<ModelInfo>();

    public IReadOnlyList<ModelInfo> VisibleAvailable { get; private set; } = Array.Empty<ModelInfo>();

    public HashSet<string> CachedAliases { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> LoadedAliases { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ModelOperationState> Operations { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, VariantSelectionState> _variantSelections = new(StringComparer.OrdinalIgnoreCase);

    public CatalogFacets Facets { get; private set; } =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Device>(), Array.Empty<string>());

    public bool IsFilterEmpty =>
        string.IsNullOrWhiteSpace(Filter.SearchText) &&
        Filter.Device is null &&
        string.IsNullOrWhiteSpace(Filter.Task) &&
        string.IsNullOrWhiteSpace(Filter.Provider) &&
        !Filter.CachedOnly;

    public bool HasActiveFilter => !IsFilterEmpty;

    /// <summary>True only when the visible set is the curated subset (curated mode AND no active filter).
    /// The curated banner and the curated data selection share this single predicate (honesty).</summary>
    public bool IsShowingCurated => ViewMode == CatalogViewMode.Curated && IsFilterEmpty;

    public void SetLoading()
    {
        Status = CatalogStatus.Loading;
        ErrorMessage = null;
    }

    public void SetError(string message)
    {
        Status = CatalogStatus.Error;
        ErrorMessage = message;
        Visible = Array.Empty<ModelInfo>();
        VisibleCached = Array.Empty<ModelInfo>();
        VisibleAvailable = Array.Empty<ModelInfo>();
    }

    public void ResetToCuratedDefault()
    {
        Filter = new CatalogFilter();
        ViewMode = CatalogViewMode.Curated;
        NlInputText = string.Empty;
        NlResult = null;
        NlSortHint = NlSortHint.None;
        Recompute();
    }

    public void Recompute()
    {
        Facets = CatalogFacets.Derive(AllModels);
        var filtered = IsShowingCurated
            ? CuratedSelector.Select(AllModels)
            : (Filter.CachedOnly
                ? (Filter with { CachedOnly = false }).Apply(AllModels).Where(model => CachedAliases.Contains(model.Alias)).ToList()
                : Filter.Apply(AllModels));

        // Apply NL sort hint on top of filter results.
        var sorted = NlSortHint switch
        {
            NlSortHint.SizeAscending => filtered.OrderBy(m => m.SizeGb ?? double.MaxValue).ToList(),
            NlSortHint.SizeDescending => filtered.OrderByDescending(m => m.SizeGb ?? 0).ToList(),
            NlSortHint.ContextDescending => filtered.OrderByDescending(m => m.ContextLength ?? 0).ToList(),
            _ => filtered,
        };

        // Apply personalization boost when enabled and a profile is available.
        // Re-sort only — every model still appears; matching models surface first.
        IReadOnlyList<ModelInfo> finalSorted = (PersonalizationActive && !PersonalizationProfile.IsEmpty)
            ? ModelRanker.Rank(PersonalizationProfile, sorted)
            : sorted;

        Visible = finalSorted;

        var groups = CatalogGrouping.Partition(Visible, CachedAliases);
        VisibleCached = groups.Cached;
        VisibleAvailable = groups.Available;

        // Empty whenever nothing is visible (no models at all, or no matches, or curated yielded nothing)
        // — never "Populated" over an empty grid.
        Status = Visible.Count == 0
            ? CatalogStatus.Empty
            : CatalogStatus.Populated;

        ErrorMessage = null;
    }

    public void SetAuthoritativeState(IEnumerable<ModelInfo> cachedModels, IEnumerable<ModelInfo> loadedModels)
    {
        CachedAliases = cachedModels
            .Select(m => m.Alias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        LoadedAliases = loadedModels
            .Select(m => m.Alias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsCached(ModelInfo model) => CachedAliases.Contains(model.Alias);

    public bool IsLoaded(ModelInfo model) => LoadedAliases.Contains(model.Alias);

    public ModelOperationState GetOperation(ModelInfo model)
    {
        if (!Operations.TryGetValue(model.Alias, out var operation))
        {
            operation = new ModelOperationState();
            Operations[model.Alias] = operation;
        }

        return operation;
    }

    public VariantSelectionState GetVariantSelection(ModelInfo model)
    {
        if (!_variantSelections.TryGetValue(model.Alias, out var selection))
        {
            selection = new VariantSelectionState(model.Variants);
            _variantSelections[model.Alias] = selection;
        }

        return selection;
    }
}
