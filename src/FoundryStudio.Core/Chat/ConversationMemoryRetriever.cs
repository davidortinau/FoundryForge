using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Chat;

/// <summary>
/// Deterministic, offline, model-free cross-session memory retriever (Phase 5, FR-mem).
///
/// Scoring formula per candidate message:
///   score = 0.75 × termOverlap + 0.25 × recencyBoost
///   • termOverlap  = |queryTerms ∩ messageTerms| / |queryTerms|   (0 when no query terms)
///   • recencyBoost = 1 / (1 + daysSinceMessage)                   (always > 0; decays with age)
///
/// Per session we pick the highest-scoring message as the representative snippet, then return the
/// top-K sessions by that score. One snippet per session avoids flooding from verbose sessions.
///
/// FUTURE — semantic memory upgrade path:
///   Microsoft.Extensions.AI.Abstractions ships a conceptual IEmbeddingGenerator; the Agent
///   Framework (Microsoft.SemanticKernel or Microsoft.Extensions.AI) can add vector-similarity
///   retrieval on top of these same MemorySnippet results once a stable, offline-capable, in-process
///   embedding provider exists. The current keyword+recency baseline is intentionally kept as the
///   shipped v1 layer: it has no network dependency, no model dependency, no uncertain package
///   restore, and is fully unit-testable. When Agent Framework memory middleware ships a stable
///   package that works with the in-process IChatClient and does NOT require a loopback socket or
///   external embedding endpoint, replace this class body and keep the MemorySnippet output contract.
///   Tracking: filed as post-v1 backlog item "P5-semantic-memory-upgrade".
/// </summary>
public sealed class ConversationMemoryRetriever
{
    // Minimal English stopwords — just enough to avoid noise from common words.
    // Kept intentionally short; more complete stopword filtering is a post-v1 quality improvement.
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with",
        "is", "was", "are", "be", "been", "being", "have", "has", "had", "do", "does", "did",
        "will", "would", "could", "should", "may", "might", "shall", "can", "need", "dare",
        "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them",
        "my", "your", "his", "its", "our", "their", "this", "that", "these", "those",
        "what", "which", "who", "whom", "how", "when", "where", "why", "not", "no", "nor",
        "so", "yet", "both", "either", "neither", "just", "also", "very", "then", "than",
        "if", "as", "by", "from", "up", "about", "into", "through", "during", "before",
        "after", "above", "below", "between", "each", "more", "most", "other", "some",
        "such", "same", "own", "few", "too", "however", "therefore"
    };

    /// <summary>
    /// Returns the top <paramref name="topK"/> relevant snippets from past sessions
    /// (excluding <paramref name="excludeSessionId"/>) scored against <paramref name="query"/>.
    /// Returns an empty list when there are no sessions, no query terms, or all sessions are excluded.
    /// </summary>
    /// <param name="query">The user's current draft / prompt.</param>
    /// <param name="sessions">All known sessions (typically from <see cref="IChatHistoryStore"/>).</param>
    /// <param name="excludeSessionId">The current session — always excluded from results.</param>
    /// <param name="topK">Maximum number of snippets to return.</param>
    /// <param name="maxSnippetChars">Maximum characters in a returned snippet.</param>
    /// <param name="now">Wall-clock reference for recency; injectable for deterministic tests.</param>
    public IReadOnlyList<MemorySnippet> Retrieve(
        string query,
        IReadOnlyList<ChatSession> sessions,
        string? excludeSessionId = null,
        int topK = 3,
        int maxSnippetChars = 300,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);

        if (sessions.Count == 0) return Array.Empty<MemorySnippet>();

        var reference = now ?? DateTimeOffset.UtcNow;
        var queryTerms = Tokenize(query);

        var results = new List<MemorySnippet>();

        foreach (var session in sessions)
        {
            if (session.Id == excludeSessionId) continue;

            var best = ScoreBestMessage(session, queryTerms, reference, maxSnippetChars);
            if (best is not null)
                results.Add(best);
        }

        return results
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.SourceDate) // recency tiebreak
            .Take(topK)
            .ToList();
    }

    private static MemorySnippet? ScoreBestMessage(
        ChatSession session,
        HashSet<string> queryTerms,
        DateTimeOffset reference,
        int maxSnippetChars)
    {
        MemorySnippet? best = null;

        foreach (var msg in session.Messages)
        {
            // Only surface user and assistant turns — system/tool turns are not useful memory snippets.
            if (msg.Role is not (ChatTurnRole.User or ChatTurnRole.Assistant)) continue;
            if (string.IsNullOrWhiteSpace(msg.Content)) continue;

            var score = ScoreMessage(msg.Content, msg.CreatedAt, queryTerms, reference);

            if (best is null || score > best.Score)
            {
                var snippet = TruncateSnippet(msg.Content, maxSnippetChars);
                best = new MemorySnippet(
                    SessionId: session.Id,
                    SessionTitle: session.Title,
                    Snippet: snippet,
                    Score: score,
                    SourceDate: msg.CreatedAt);
            }
        }

        return best;
    }

    public static double ScoreMessage(
        string content,
        DateTimeOffset createdAt,
        HashSet<string> queryTerms,
        DateTimeOffset reference)
    {
        var termOverlap = ComputeTermOverlap(content, queryTerms);
        var daysSince = Math.Max(0, (reference - createdAt).TotalDays);
        var recencyBoost = 1.0 / (1.0 + daysSince);

        return 0.75 * termOverlap + 0.25 * recencyBoost;
    }

    public static double ComputeTermOverlap(string content, HashSet<string> queryTerms)
    {
        if (queryTerms.Count == 0) return 0.0;

        var contentTerms = Tokenize(content);
        if (contentTerms.Count == 0) return 0.0;

        var matches = queryTerms.Count(t => contentTerms.Contains(t));
        return (double)matches / queryTerms.Count;
    }

    public static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = new System.Text.StringBuilder();

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                var token = current.ToString();
                if (token.Length >= 3 && !Stopwords.Contains(token))
                    tokens.Add(token);
                current.Clear();
            }
        }

        if (current.Length >= 3)
        {
            var token = current.ToString();
            if (!Stopwords.Contains(token))
                tokens.Add(token);
        }

        return tokens;
    }

    private static string TruncateSnippet(string content, int maxChars)
    {
        var trimmed = content.Trim();
        if (trimmed.Length <= maxChars) return trimmed;

        // Break at a word boundary to avoid cutting mid-word.
        var cut = trimmed.LastIndexOf(' ', maxChars);
        return cut > 0
            ? trimmed[..cut] + "…"
            : trimmed[..maxChars] + "…";
    }
}
