using FoundryForge.Core.Models;

namespace FoundryForge.Core.Catalog;

/// <summary>
/// Honest facet option derivation (M2, R4, FR-006). Produces the distinct, non-empty Task/Provider/Device
/// options actually present in the catalog so filter controls never offer a value no model has. Models with
/// an empty/unknown facet are EXCLUDED from that facet's options (never bucketed as a real value).
/// </summary>
public sealed record CatalogFacets(
    IReadOnlyList<string> Tasks,
    IReadOnlyList<string> Providers,
    IReadOnlyList<Device> Devices,
    IReadOnlyList<string> SearchTokens)
{
    public static CatalogFacets Derive(IEnumerable<ModelInfo> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        var list = models.ToList();

        var tasks = list
            .Select(m => m.Task)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var providers = list
            .Select(m => m.Provider)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var devices = list
            .Select(m => m.Device)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        // Real, searchable term corpus: every token that appears in an alias / display name / id.
        // Used to validate NL remainder words so we never search for a term no model actually has
        // (same honesty rule as the facets above) — filler like "inspect"/"them" is dropped, not
        // joined into a literal query that zeroes out otherwise-valid results.
        var searchTokens = list
            .SelectMany(m => Tokenize(m.Alias).Concat(Tokenize(m.DisplayName)).Concat(Tokenize(m.Id)))
            .Where(t => t.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CatalogFacets(tasks, providers, devices, searchTokens);
    }

    private static readonly char[] TokenSeparators =
        [' ', '-', '_', '.', ',', '/', '\\', ':', '(', ')', '[', ']'];

    private static IEnumerable<string> Tokenize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.ToLowerInvariant().Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
