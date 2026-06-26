using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using Device = FoundryStudio.Core.Models.Device;

namespace FoundryStudio.App.Components.Catalog;

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

    public IReadOnlyList<ModelInfo> Visible { get; private set; } = Array.Empty<ModelInfo>();

    public CatalogFacets Facets { get; private set; } =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Device>());

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
    }

    public void ResetToCuratedDefault()
    {
        Filter = new CatalogFilter();
        ViewMode = CatalogViewMode.Curated;
        Recompute();
    }

    public void Recompute()
    {
        Facets = CatalogFacets.Derive(AllModels);
        Visible = IsShowingCurated
            ? CuratedSelector.Select(AllModels)
            : Filter.Apply(AllModels);

        // Empty whenever nothing is visible (no models at all, or no matches, or curated yielded nothing)
        // — never "Populated" over an empty grid.
        Status = Visible.Count == 0
            ? CatalogStatus.Empty
            : CatalogStatus.Populated;

        ErrorMessage = null;
    }
}
