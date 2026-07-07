using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Catalog;

/// <summary>
/// Deterministic NL → CatalogFilter interpreter (M5 / Phase 3).
///
/// Pure logic, no MAUI, no model dependency — works fully offline and is unit-testable.
/// Maps keyword signals to REAL facet values only: never invents a value no model has.
///
/// Design: scan the lowercased query for keyword groups in priority order, consuming tokens
/// so a word does not map to two things.  Any tokens left over go into SearchText so they still
/// narrow by alias/name.  The result also carries a sort hint (size or context) and a list of
/// human-readable chips that reflect exactly what was inferred.
/// </summary>
public sealed class NaturalLanguageQueryInterpreter
{
    // ── Device keywords ──────────────────────────────────────────────────────────
    private static readonly string[] GpuKeywords = ["gpu", "webgpu", "graphics"];
    private static readonly string[] CpuKeywords = ["cpu"];
    private static readonly string[] NpuKeywords = ["npu"];

    // ── Capability keywords ───────────────────────────────────────────────────────
    private static readonly string[] VisionKeywords = ["vision", "image", "images", "multimodal", "visual", "picture", "photo", "pictures", "photos"];
    private static readonly string[] ToolUseKeywords = ["tool", "tools", "agent", "agents", "function", "functions", "agentic", "function-calling"];
    private static readonly string[] ReasoningKeywords = ["reason", "reasoning", "math", "logic", "logical", "think", "thinking", "cot", "chain-of-thought"];

    // ── Sort / size keywords ──────────────────────────────────────────────────────
    private static readonly string[] SmallSizeKeywords = ["small", "fast", "tiny", "lightweight", "compact", "lite", "mini", "efficient"];
    private static readonly string[] LargeSizeKeywords = ["large", "powerful", "biggest", "best quality", "high quality", "quality", "capable"];
    private static readonly string[] LongContextKeywords = ["long context", "large context", "big context", "long-context", "large-context", "high context", "128k", "256k", "context window"];

    // ── Common stop words to skip ─────────────────────────────────────────────────
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "for", "on", "to", "in", "of", "with", "that", "is", "i", "me",
        "my", "need", "want", "give", "get", "find", "show", "model", "models", "local",
        "something", "please", "good", "use", "using", "which", "what", "can", "do", "does",
    };

    /// <summary>
    /// Interprets <paramref name="query"/> into a <see cref="NlQueryResult"/>.
    /// <paramref name="availableTasks"/> and <paramref name="availableProviders"/> come from
    /// <see cref="CatalogFacets"/> so matched values are guaranteed to exist in the catalog.
    /// </summary>
    public NlQueryResult Interpret(
        string query,
        IReadOnlyList<string> availableTasks,
        IReadOnlyList<string> availableProviders,
        IReadOnlyList<string>? availableSearchTerms = null)
    {
        ArgumentNullException.ThrowIfNull(availableTasks);
        ArgumentNullException.ThrowIfNull(availableProviders);

        if (string.IsNullOrWhiteSpace(query))
        {
            return NlQueryResult.Empty;
        }

        var chips = new List<NlInferredChip>();
        Device? device = null;
        string? task = null;
        string? provider = null;
        var capabilityHints = new List<string>();
        var sortHint = NlSortHint.None;

        // Normalise — work on the lowercased original text for multi-word phrase matches,
        // then also split into individual tokens for single-word keyword matching.
        var lower = query.Trim().ToLowerInvariant();

        // ── 1. Multi-word phrase matches first (before tokenising) ────────────────
        foreach (var phrase in LongContextKeywords)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                sortHint = NlSortHint.ContextDescending;
                lower = lower.Replace(phrase, " ", StringComparison.Ordinal);
                break;
            }
        }

        foreach (var phrase in LargeSizeKeywords)
        {
            if (phrase.Contains(' ') && lower.Contains(phrase, StringComparison.Ordinal))
            {
                sortHint = NlSortHint.SizeDescending;
                lower = lower.Replace(phrase, " ", StringComparison.Ordinal);
                break;
            }
        }

        // ── 2. Split remaining text into tokens ───────────────────────────────────
        var tokens = new HashSet<string>(
            lower.Split([' ', ',', '.', '?', '!', ';', ':', '-', '_', '/', '\\'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);

        // ── 3. Device ─────────────────────────────────────────────────────────────
        if (tokens.Overlaps(GpuKeywords))
        {
            device = Device.Gpu;
            ConsumeAll(tokens, GpuKeywords);
        }
        else if (tokens.Overlaps(CpuKeywords))
        {
            device = Device.Cpu;
            ConsumeAll(tokens, CpuKeywords);
        }
        else if (tokens.Overlaps(NpuKeywords))
        {
            device = Device.Npu;
            ConsumeAll(tokens, NpuKeywords);
        }

        // ── 4. Capabilities → capability hints only (not a CatalogFilter field) ──
        if (tokens.Overlaps(VisionKeywords))
        {
            capabilityHints.Add("vision");
            ConsumeAll(tokens, VisionKeywords);
        }

        if (tokens.Overlaps(ToolUseKeywords))
        {
            capabilityHints.Add("tool use");
            ConsumeAll(tokens, ToolUseKeywords);
        }

        if (tokens.Overlaps(ReasoningKeywords))
        {
            capabilityHints.Add("reasoning");
            ConsumeAll(tokens, ReasoningKeywords);
        }

        // ── 5. Size / speed sort (single-word phrases) ────────────────────────────
        if (sortHint == NlSortHint.None)
        {
            if (tokens.Overlaps(SmallSizeKeywords))
            {
                sortHint = NlSortHint.SizeAscending;
                ConsumeAll(tokens, SmallSizeKeywords);
            }
            else if (tokens.Overlaps(LargeSizeKeywords.Where(k => !k.Contains(' ')).ToArray()))
            {
                sortHint = NlSortHint.SizeDescending;
                ConsumeAll(tokens, LargeSizeKeywords.Where(k => !k.Contains(' ')).ToArray());
            }
        }
        else
        {
            // Already matched a multi-word long-context or large-size phrase; consume remaining tokens
            if (sortHint == NlSortHint.SizeDescending)
            {
                ConsumeAll(tokens, LargeSizeKeywords.Where(k => !k.Contains(' ')).ToArray());
            }
        }

        // ── 6. Task — case-insensitive substring match against real facet values ──
        foreach (var candidate in availableTasks)
        {
            // Check if any token is contained in the candidate task string or vice-versa
            if (tokens.Any(t => t.Length >= 3 &&
                    candidate.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                task = candidate;
                // Consume matched tokens
                var matched = tokens.Where(t => t.Length >= 3 &&
                    candidate.Contains(t, StringComparison.OrdinalIgnoreCase)).ToArray();
                ConsumeAll(tokens, matched);
                break;
            }
        }

        // Also try capability hints as task hints (e.g. "coding" → "code-generation")
        // If we have a capability hint and no task yet, use capability token to check tasks
        if (task is null)
        {
            foreach (var hint in new[] { "cod", "code", "coding", "program", "chat", "summar", "embed", "translat" })
            {
                if (tokens.Any(t => t.StartsWith(hint, StringComparison.OrdinalIgnoreCase)))
                {
                    var taskMatch = availableTasks.FirstOrDefault(candidate =>
                        candidate.Contains(hint, StringComparison.OrdinalIgnoreCase));
                    if (taskMatch is not null)
                    {
                        task = taskMatch;
                        var matched = tokens.Where(t => t.StartsWith(hint, StringComparison.OrdinalIgnoreCase)).ToArray();
                        ConsumeAll(tokens, matched);
                        break;
                    }
                }
            }
        }

        // ── 7. Provider — substring match against real facet values ──────────────
        foreach (var candidate in availableProviders)
        {
            var candidateLower = candidate.ToLowerInvariant();
            if (tokens.Any(t => t.Length >= 3 &&
                    (candidateLower.Contains(t, StringComparison.Ordinal) ||
                     t.Contains(candidateLower, StringComparison.Ordinal))))
            {
                provider = candidate;
                var matched = tokens.Where(t => t.Length >= 3 &&
                    (candidateLower.Contains(t, StringComparison.Ordinal) ||
                     t.Contains(candidateLower, StringComparison.Ordinal))).ToArray();
                ConsumeAll(tokens, matched);
                break;
            }
        }

        // ── 8. Remaining tokens → SearchText ──────────────────────────────────────
        // Drop stop-words, then — when we know the catalog's real search corpus — keep ONLY tokens that
        // actually match some alias/name/id term. This prevents filler words the interpreter didn't map
        // (e.g. "inspect", "load", "them") from being joined into a literal phrase that matches no model
        // and zeroes out otherwise-valid facet/capability results. Same honesty rule as CatalogFacets:
        // never search for a term no model has. When no corpus is supplied, fall back to the prior
        // behavior (keep non-stop-words) so callers without facet context still function.
        var hasCorpus = availableSearchTerms is { Count: > 0 };
        var remainder = tokens
            .Where(t => !StopWords.Contains(t) && t.Length >= 2)
            .Where(t => !hasCorpus || MatchesCatalog(t, availableSearchTerms!))
            .OrderBy(t => t)
            .ToList();
        var searchText = remainder.Count > 0 ? string.Join(" ", remainder) : null;

        // ── 9. Build chips ────────────────────────────────────────────────────────
        if (device is not null)
        {
            chips.Add(new NlInferredChip($"Device: {device}", device.Value.ToString(), NlChipKind.Device));
        }

        if (task is not null)
        {
            chips.Add(new NlInferredChip($"Task: {task}", task, NlChipKind.Task));
        }

        if (provider is not null)
        {
            chips.Add(new NlInferredChip($"Provider: {provider}", provider, NlChipKind.Provider));
        }

        foreach (var cap in capabilityHints)
        {
            chips.Add(new NlInferredChip($"Capability: {cap}", cap, NlChipKind.Capability));
        }

        if (sortHint != NlSortHint.None)
        {
            var sortLabel = sortHint switch
            {
                NlSortHint.SizeAscending => "Smaller first",
                NlSortHint.SizeDescending => "Larger first",
                NlSortHint.ContextDescending => "Long context first",
                _ => string.Empty,
            };
            chips.Add(new NlInferredChip(sortLabel, sortHint.ToString(), NlChipKind.SortHint));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            chips.Add(new NlInferredChip($"Search: {searchText}", searchText, NlChipKind.SearchText));
        }

        if (chips.Count == 0)
        {
            return NlQueryResult.Empty;
        }

        var filter = new CatalogFilter(
            Device: device,
            Task: task,
            SearchText: searchText);

        return new NlQueryResult(filter, sortHint, chips, capabilityHints);
    }

    private static void ConsumeAll(HashSet<string> tokens, IEnumerable<string> keywords)
    {
        foreach (var kw in keywords)
        {
            tokens.Remove(kw);
        }
    }

    // A remainder token counts as a real search term only if it substring-overlaps some catalog term
    // (alias/name/id token). e.g. "qwen"/"phi" match; "inspect"/"them" match nothing and are dropped.
    private static bool MatchesCatalog(string token, IReadOnlyList<string> catalogTerms)
    {
        foreach (var term in catalogTerms)
        {
            if (term.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                token.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Result of a natural-language query interpretation.</summary>
public sealed record NlQueryResult(
    CatalogFilter Filter,
    NlSortHint SortHint,
    IReadOnlyList<NlInferredChip> Chips,
    IReadOnlyList<string> CapabilityHints)
{
    public static readonly NlQueryResult Empty =
        new(new CatalogFilter(), NlSortHint.None, Array.Empty<NlInferredChip>(), Array.Empty<string>());

    public bool IsEmpty => Chips.Count == 0;
}

/// <summary>A single inferred filter fact, shown as a removable chip in the UI.</summary>
public sealed record NlInferredChip(
    string Label,
    string Value,
    NlChipKind Kind);

/// <summary>The dimension this chip represents (determines which filter field to clear on removal).</summary>
public enum NlChipKind
{
    Device,
    Task,
    Provider,
    Capability,
    SortHint,
    SearchText,
}

/// <summary>How the NL interpreter wants the catalog sorted after filtering.</summary>
public enum NlSortHint
{
    None,
    /// <summary>Prefer smaller models (SizeGb ascending). "small/fast/tiny/lightweight".</summary>
    SizeAscending,
    /// <summary>Prefer larger models (SizeGb descending). "large/powerful/best quality".</summary>
    SizeDescending,
    /// <summary>Prefer longer context windows (ContextLength descending). "long context".</summary>
    ContextDescending,
}
