using System.Text.Json;
using FoundryStudio.Core.Models;
using Microsoft.Extensions.AI;

namespace FoundryStudio.Core.Catalog;

/// <summary>
/// AI-backed <see cref="INlQueryInterpreter"/> over any Microsoft.Extensions.AI <c>IChatClient</c>
/// (Apple Intelligence, a local Foundry model, or Copilot CLI). It asks the model to extract a strict
/// JSON structure, then maps + <b>validates every value against the real catalog facets</b> so the model
/// can never introduce a device/task/term no model has (honesty rule). Any failure — unavailable model,
/// bad JSON, or timeout — falls back to the deterministic interpreter so search never breaks.
/// </summary>
public sealed class ChatModelNlInterpreter : INlQueryInterpreter
{
    private static readonly string[] KnownCapabilities = ["vision", "tools", "reasoning"];

    private readonly IChatClient _chat;
    private readonly INlQueryInterpreter _fallback;
    private readonly TimeSpan _timeout;
    private readonly string? _modelId;

    public ChatModelNlInterpreter(
        IChatClient chat,
        INlQueryInterpreter fallback,
        TimeSpan? timeout = null,
        string? modelId = null)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _timeout = timeout ?? TimeSpan.FromSeconds(8);
        _modelId = modelId;
    }

    public async Task<NlQueryResult> InterpretAsync(string query, CatalogFacets facets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facets);
        if (string.IsNullOrWhiteSpace(query))
        {
            return NlQueryResult.Empty;
        }

        string text;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            var options = new ChatOptions { Temperature = 0f };
            if (_modelId is not null)
            {
                options.ModelId = _modelId;
            }

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, SystemPrompt(facets)),
                new ChatMessage(ChatRole.User, query.Trim()),
            };

            var response = await _chat.GetResponseAsync(messages, options, timeoutCts.Token).ConfigureAwait(false);
            text = response.Text ?? string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller cancelled (e.g. a newer submit) — propagate, don't mask as a fallback.
            throw;
        }
        catch (Exception)
        {
            // Timeout, model unavailable, transport error — degrade to the instant engine.
            return await _fallback.InterpretAsync(query, facets, cancellationToken).ConfigureAwait(false);
        }

        var parsed = TryParse(text);
        if (parsed is null)
        {
            return await _fallback.InterpretAsync(query, facets, cancellationToken).ConfigureAwait(false);
        }

        var result = MapAndValidate(parsed.Value, facets);

        // If the model extracted nothing usable, defer to the deterministic engine rather than showing
        // an empty interpretation for a query the keyword pass might still handle.
        return result.IsEmpty
            ? await _fallback.InterpretAsync(query, facets, cancellationToken).ConfigureAwait(false)
            : result;
    }

    private static string SystemPrompt(CatalogFacets facets)
    {
        var tasks = facets.Tasks.Count > 0 ? string.Join(", ", facets.Tasks) : "(none)";
        var providers = facets.Providers.Count > 0 ? string.Join(", ", facets.Providers) : "(none)";
        var devices = facets.Devices.Count > 0
            ? string.Join(", ", facets.Devices.Select(d => d.ToString().ToLowerInvariant()))
            : "(none)";

        return $$"""
            You extract structured search filters for a local LLM model catalog. Respond with ONLY a
            single JSON object, no prose, no markdown code fence.

            Schema (every field optional; OMIT or use null when the user did not clearly imply it):
            {
              "device": one of [{{devices}}] or null,
              "task": one of [{{tasks}}] or null,
              "provider": one of [{{providers}}] or null,
              "capabilities": array of any of ["vision","tools","reasoning"],
              "sort": one of ["size-asc","size-desc","context-desc"] or null,
              "keywords": array of concrete model/family name fragments (e.g. "qwen","phi","llama")
            }

            Hard rules:
            - Do NOT guess. Only set a field when the user's words clearly imply it. When unsure, use null / omit.
            - A search box needs RECALL: prefer FEWER filters. Set "device", "task", and "provider" ONLY when
              the user explicitly named them (e.g. said "gpu", "for coding", or a provider name). Never infer
              them from vague intent — a wrong filter that returns nothing is far worse than no filter.
            - "capabilities" and "sort" are safe to infer from intent; "device"/"task"/"provider" are not.
            - "keywords" must contain ONLY concrete model or family name fragments. NEVER put filler verbs
              or generic words (want, load, need, inspect, use, run, trigger, small, fast, images, them,
              reasoning, chat) in keywords.
            - Prefer null over a weak guess. An empty result is better than a wrong filter.
            """;
    }

    private readonly record struct ParsedQuery(
        string? Device,
        string? Task,
        string? Provider,
        IReadOnlyList<string> Capabilities,
        string? Sort,
        IReadOnlyList<string> Keywords);

    private static ParsedQuery? TryParse(string text)
    {
        var json = ExtractJsonObject(text);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new ParsedQuery(
                Device: GetString(root, "device"),
                Task: GetString(root, "task"),
                Provider: GetString(root, "provider"),
                Capabilities: GetStringArray(root, "capabilities"),
                Sort: GetString(root, "sort"),
                Keywords: GetStringArray(root, "keywords"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Isolate the first balanced {...} so a stray fence or trailing prose can't break parsing.
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
        }

        return null;
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            return string.IsNullOrWhiteSpace(s) || string.Equals(s, "null", StringComparison.OrdinalIgnoreCase) ? null : s.Trim();
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
            {
                list.Add(s.Trim());
            }
        }

        return list;
    }

    private static NlQueryResult MapAndValidate(ParsedQuery parsed, CatalogFacets facets)
    {
        var chips = new List<NlInferredChip>();

        // Device — must be a real facet value.
        Device? device = null;
        if (parsed.Device is { } dstr && Enum.TryParse<Device>(dstr, ignoreCase: true, out var d) && facets.Devices.Contains(d))
        {
            device = d;
            chips.Add(new NlInferredChip($"Device: {d}", d.ToString(), NlChipKind.Device));
        }

        // Task — match against real facet values (case-insensitive).
        string? task = null;
        if (parsed.Task is { } tstr)
        {
            task = facets.Tasks.FirstOrDefault(t => string.Equals(t, tstr, StringComparison.OrdinalIgnoreCase))
                ?? facets.Tasks.FirstOrDefault(t => t.Contains(tstr, StringComparison.OrdinalIgnoreCase));
            if (task is not null)
            {
                chips.Add(new NlInferredChip($"Task: {task}", task, NlChipKind.Task));
            }
        }

        // Provider — must be a real facet value.
        string? provider = null;
        if (parsed.Provider is { } pstr)
        {
            provider = facets.Providers.FirstOrDefault(p => string.Equals(p, pstr, StringComparison.OrdinalIgnoreCase));
            if (provider is not null)
            {
                chips.Add(new NlInferredChip($"Provider: {provider}", provider, NlChipKind.Provider));
            }
        }

        // Capabilities — display-only hints, restricted to the known set.
        var capabilityHints = new List<string>();
        foreach (var cap in parsed.Capabilities)
        {
            var norm = cap.Trim().ToLowerInvariant();
            norm = norm switch
            {
                "tool" or "tool use" or "tool-use" or "function" or "functions" => "tools",
                "reason" => "reasoning",
                _ => norm,
            };
            if (KnownCapabilities.Contains(norm) && !capabilityHints.Contains(norm))
            {
                capabilityHints.Add(norm);
                chips.Add(new NlInferredChip($"Capability: {norm}", norm, NlChipKind.Capability));
            }
        }

        // Sort hint.
        var sortHint = parsed.Sort?.ToLowerInvariant() switch
        {
            "size-asc" => NlSortHint.SizeAscending,
            "size-desc" => NlSortHint.SizeDescending,
            "context-desc" => NlSortHint.ContextDescending,
            _ => NlSortHint.None,
        };
        if (sortHint != NlSortHint.None)
        {
            var label = sortHint switch
            {
                NlSortHint.SizeAscending => "Smaller first",
                NlSortHint.SizeDescending => "Larger first",
                NlSortHint.ContextDescending => "Long context first",
                _ => string.Empty,
            };
            chips.Add(new NlInferredChip(label, sortHint.ToString(), NlChipKind.SortHint));
        }

        // Keywords -> SearchText, keeping only tokens that clearly name a real catalog term. Stricter than
        // the deterministic pass: an AI keyword must be a whole catalog token or a prefix of one (>=3 chars),
        // so filler the model wrongly emitted as a "keyword" (e.g. "small", "reasoning") can't over-constrain.
        var kept = parsed.Keywords
            .Select(k => k.Trim().ToLowerInvariant())
            .Where(k => k.Length >= 3 && MatchesCatalogStrict(k, facets.SearchTokens))
            .Distinct()
            .OrderBy(k => k)
            .ToList();
        var searchText = kept.Count > 0 ? string.Join(" ", kept) : null;
        if (searchText is not null)
        {
            chips.Add(new NlInferredChip($"Search: {searchText}", searchText, NlChipKind.SearchText));
        }

        if (chips.Count == 0)
        {
            return NlQueryResult.Empty;
        }

        var filter = new CatalogFilter(Device: device, Task: task, Provider: provider, SearchText: searchText);
        return new NlQueryResult(filter, sortHint, chips, capabilityHints);
    }

    // Stricter: the token must be a whole catalog term or a genuine prefix of one (a model/family name
    // fragment like "qwen", "phi", "mistr"), not any incidental substring. Keeps hallucinated filler out.
    private static bool MatchesCatalogStrict(string token, IReadOnlyList<string> catalogTerms)
    {
        foreach (var term in catalogTerms)
        {
            if (term.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                term.StartsWith(token, StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
