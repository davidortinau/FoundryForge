using FoundryForge.Core.Models;

namespace FoundryForge.Core.Catalog;

/// <summary>
/// Deterministic curated default view (M2, R3, FR-008). Returns models whose alias is in the
/// FoundryForge-curated allow-list, in allow-list order. This makes NO quality claim — it is an
/// app-defined, documented, adjustable starter set of small/common models so a first-run user is not
/// dropped into the full catalog. Curated aliases absent from the live catalog are silently skipped.
/// </summary>
public static class CuratedSelector
{
    /// <summary>
    /// FoundryForge-curated starter aliases (documented, adjustable; no quality ranking implied).
    /// Chosen as small, broadly-useful on-device models likely present in the FL catalog.
    /// </summary>
    public static IReadOnlyList<string> CuratedAliases { get; } = new[]
    {
        "qwen2.5-0.5b",
        "qwen2.5-1.5b",
        "qwen2.5-7b",
        "phi-3.5-mini",
        "phi-4-mini",
        "deepseek-r1-distill-qwen-7b",
        "mistral-7b-v0.2",
    };

    public static IReadOnlyList<ModelInfo> Select(IEnumerable<ModelInfo> all)
    {
        ArgumentNullException.ThrowIfNull(all);
        var byAlias = all
            .GroupBy(m => m.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<ModelInfo>();
        foreach (var alias in CuratedAliases)
        {
            if (byAlias.TryGetValue(alias, out var model))
            {
                result.Add(model);
            }
        }

        return result;
    }
}
