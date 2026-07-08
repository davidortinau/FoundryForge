namespace FoundryForge.Core.Catalog;

/// <summary>
/// Instant, offline <see cref="INlQueryInterpreter"/> wrapping the deterministic keyword interpreter.
/// Always available, sub-millisecond, no model. Serves as the baseline engine and the fallback for the
/// AI-backed engines when a model is unavailable, errors, or times out.
/// </summary>
public sealed class DeterministicNlInterpreter : INlQueryInterpreter
{
    private readonly NaturalLanguageQueryInterpreter _inner = new();

    public Task<NlQueryResult> InterpretAsync(string query, CatalogFacets facets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facets);
        var result = _inner.Interpret(query, facets.Tasks, facets.Providers, facets.SearchTokens);
        return Task.FromResult(result);
    }
}
