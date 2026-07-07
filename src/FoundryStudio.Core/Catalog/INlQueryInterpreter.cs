namespace FoundryStudio.Core.Catalog;

/// <summary>
/// Interprets a natural-language catalog query into a <see cref="NlQueryResult"/> (filter + sort + chips).
/// Two families implement this: the instant, offline <see cref="DeterministicNlInterpreter"/> and the
/// AI-backed <see cref="ChatModelNlInterpreter"/> (any <c>IChatClient</c> — Apple Intelligence, a local
/// Foundry model, or Copilot CLI). All results are validated against the real catalog facets so no engine
/// can invent a value no model has (honesty rule).
/// </summary>
public interface INlQueryInterpreter
{
    Task<NlQueryResult> InterpretAsync(string query, CatalogFacets facets, CancellationToken cancellationToken = default);
}
