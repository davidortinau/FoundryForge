namespace FoundryForge.Core.Chat;

/// <summary>
/// A single relevant snippet surfaced from a past conversation by <see cref="ConversationMemoryRetriever"/>.
/// Provenance is always visible to the user — never silently injected (Design §9 / honesty rules).
/// </summary>
public sealed record MemorySnippet(
    string SessionId,
    string SessionTitle,
    string Snippet,
    double Score,
    DateTimeOffset SourceDate);
