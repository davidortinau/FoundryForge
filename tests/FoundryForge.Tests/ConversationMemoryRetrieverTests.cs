using FoundryForge.Core.Chat;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

/// <summary>
/// Phase 5 — ConversationMemoryRetriever: deterministic, offline, model-free cross-session memory.
/// All tests are fully dylib-free (pure logic, no file I/O, no network).
/// </summary>
public class ConversationMemoryRetrieverTests
{
    private static readonly DateTimeOffset BaseNow = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static ChatSession MakeSession(
        string id,
        string title,
        IReadOnlyList<ChatMessageRecord> messages,
        DateTimeOffset? updatedAt = null)
    {
        var at = updatedAt ?? BaseNow;
        return new ChatSession(
            Id: id,
            Title: title,
            SystemPrompt: null,
            Parameters: InferenceParameters.Defaults,
            ModelAlias: null,
            Messages: messages,
            CreatedAt: at,
            UpdatedAt: at);
    }

    private static ChatMessageRecord UserMsg(string content, DateTimeOffset? at = null) =>
        new(ChatTurnRole.User, content, at ?? BaseNow);

    private static ChatMessageRecord AssistantMsg(string content, DateTimeOffset? at = null) =>
        new(ChatTurnRole.Assistant, content, at ?? BaseNow);

    private readonly ConversationMemoryRetriever _retriever = new();

    // -------------------------------------------------------------------------
    // 1. Empty history → empty result
    // -------------------------------------------------------------------------

    [Fact]
    public void EmptyHistory_ReturnsEmpty()
    {
        var result = _retriever.Retrieve("anything", Array.Empty<ChatSession>(), topK: 3, now: BaseNow);
        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // 2. Current session excluded
    // -------------------------------------------------------------------------

    [Fact]
    public void CurrentSession_IsExcluded()
    {
        var session = MakeSession("current", "Current",
            new[] { UserMsg("machine learning model training") });
        var sessions = new[] { session };

        var result = _retriever.Retrieve("machine learning", sessions,
            excludeSessionId: "current", topK: 3, now: BaseNow);

        Assert.Empty(result);
    }

    [Fact]
    public void OtherSession_IsNotExcluded()
    {
        var current = MakeSession("current", "Current",
            new[] { UserMsg("machine learning model training") });
        var other = MakeSession("other", "Past research",
            new[] { UserMsg("machine learning and neural networks") });

        var result = _retriever.Retrieve("machine learning", new[] { current, other },
            excludeSessionId: "current", topK: 3, now: BaseNow);

        Assert.Single(result);
        Assert.Equal("other", result[0].SessionId);
    }

    // -------------------------------------------------------------------------
    // 3. Relevant snippet ranks above irrelevant
    // -------------------------------------------------------------------------

    [Fact]
    public void RelevantMessage_RanksAboveIrrelevant()
    {
        var relevant = MakeSession("rel", "Relevant",
            new[] { UserMsg("machine learning and neural networks for image recognition") });
        var irrelevant = MakeSession("irr", "Irrelevant",
            new[] { UserMsg("grocery shopping list and meal planning") });

        var result = _retriever.Retrieve(
            query: "machine learning neural",
            sessions: new[] { relevant, irrelevant },
            excludeSessionId: null,
            topK: 2,
            now: BaseNow);

        Assert.Equal(2, result.Count);
        Assert.Equal("rel", result[0].SessionId);
        Assert.True(result[0].Score > result[1].Score);
    }

    // -------------------------------------------------------------------------
    // 4. K limit respected
    // -------------------------------------------------------------------------

    [Fact]
    public void TopK_LimitsResults()
    {
        var sessions = Enumerable.Range(1, 10)
            .Select(i => MakeSession($"s{i}", $"Session {i}",
                new[] { UserMsg($"machine learning topic number {i}") }))
            .ToList();

        var result = _retriever.Retrieve("machine learning", sessions, topK: 3, now: BaseNow);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void TopK_ReturnsFewerWhenNotEnoughSessions()
    {
        var sessions = new[]
        {
            MakeSession("a", "A", new[] { UserMsg("cats and dogs") }),
            MakeSession("b", "B", new[] { UserMsg("weather and rain") })
        };

        var result = _retriever.Retrieve("cats dogs rain", sessions, topK: 5, now: BaseNow);

        Assert.Equal(2, result.Count);
    }

    // -------------------------------------------------------------------------
    // 5. Recency tiebreak — equally relevant, newer wins
    // -------------------------------------------------------------------------

    [Fact]
    public void Recency_Tiebreak_NewerSessionWins()
    {
        var oldDate = BaseNow.AddDays(-60);
        var newDate = BaseNow.AddDays(-1);

        // Same content → same term overlap → recency decides
        var older = MakeSession("old", "Older",
            new[] { UserMsg("python programming tutorial", oldDate) }, updatedAt: oldDate);
        var newer = MakeSession("new", "Newer",
            new[] { UserMsg("python programming tutorial", newDate) }, updatedAt: newDate);

        var result = _retriever.Retrieve(
            "python programming tutorial",
            new[] { older, newer },
            topK: 2,
            now: BaseNow);

        Assert.Equal(2, result.Count);
        Assert.Equal("new", result[0].SessionId);
    }

    // -------------------------------------------------------------------------
    // 6. Result fields are populated correctly
    // -------------------------------------------------------------------------

    [Fact]
    public void Result_ContainsCorrectProvenance()
    {
        var msgDate = BaseNow.AddDays(-5);
        var session = MakeSession("s1", "My Research",
            new[] { UserMsg("machine learning experiments with transformers", msgDate) });

        var result = _retriever.Retrieve("machine learning transformers",
            new[] { session }, topK: 1, now: BaseNow);

        Assert.Single(result);
        Assert.Equal("s1", result[0].SessionId);
        Assert.Equal("My Research", result[0].SessionTitle);
        Assert.NotEmpty(result[0].Snippet);
        Assert.True(result[0].Score > 0);
        Assert.Equal(msgDate, result[0].SourceDate);
    }

    // -------------------------------------------------------------------------
    // 7. Empty query → no term overlap, recency-only ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void EmptyQuery_ReturnsSessionsByRecency()
    {
        var old = MakeSession("old", "Old",
            new[] { UserMsg("hello world", BaseNow.AddDays(-30)) });
        var recent = MakeSession("recent", "Recent",
            new[] { UserMsg("hello world", BaseNow.AddDays(-1)) });

        var result = _retriever.Retrieve("",
            new[] { old, recent },
            topK: 2,
            now: BaseNow);

        // Both have 0 term overlap; recency decides.
        Assert.Equal(2, result.Count);
        Assert.Equal("recent", result[0].SessionId);
    }

    // -------------------------------------------------------------------------
    // 8. System/tool messages are excluded from snippets
    // -------------------------------------------------------------------------

    [Fact]
    public void SystemAndToolMessages_AreNotSnippeted()
    {
        var session = MakeSession("s1", "S1",
            new[]
            {
                new ChatMessageRecord(ChatTurnRole.System, "system instructions here", BaseNow),
                new ChatMessageRecord(ChatTurnRole.Tool, "tool output here", BaseNow),
            });

        var result = _retriever.Retrieve("system instructions",
            new[] { session }, topK: 1, now: BaseNow);

        // No user/assistant messages → no snippet surfaced.
        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // 9. Long snippet is truncated
    // -------------------------------------------------------------------------

    [Fact]
    public void LongSnippet_IsTruncatedAtWordBoundary()
    {
        var longText = string.Join(" ", Enumerable.Range(1, 200).Select(_ => "interesting"));
        var session = MakeSession("s1", "S1",
            new[] { UserMsg(longText) });

        var result = _retriever.Retrieve("interesting",
            new[] { session }, topK: 1, maxSnippetChars: 100, now: BaseNow);

        Assert.Single(result);
        Assert.True(result[0].Snippet.Length <= 105); // word-boundary cut + "…" = slightly over 100
        Assert.EndsWith("…", result[0].Snippet);
    }

    // -------------------------------------------------------------------------
    // 10. Best message per session chosen (highest overlap wins)
    // -------------------------------------------------------------------------

    [Fact]
    public void BestMessagePerSession_SelectedAsSnippet()
    {
        var session = MakeSession("s1", "S1",
            new[]
            {
                UserMsg("grocery shopping list"),        // not relevant
                UserMsg("machine learning and deep learning neural networks"), // most relevant
                AssistantMsg("yes machine learning is great"), // partially relevant
            });

        var result = _retriever.Retrieve("machine learning deep learning",
            new[] { session }, topK: 1, now: BaseNow);

        Assert.Single(result);
        Assert.Contains("machine learning and deep learning", result[0].Snippet);
    }

    // -------------------------------------------------------------------------
    // 11. No sessions after exclusion → empty
    // -------------------------------------------------------------------------

    [Fact]
    public void AllSessionsExcluded_ReturnsEmpty()
    {
        var session = MakeSession("only", "Only",
            new[] { UserMsg("machine learning") });

        var result = _retriever.Retrieve("machine learning",
            new[] { session },
            excludeSessionId: "only",
            topK: 3,
            now: BaseNow);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // 12. Tokenizer: stopwords and short tokens filtered
    // -------------------------------------------------------------------------

    [Fact]
    public void Tokenizer_FiltersStopwordsAndShortTokens()
    {
        var tokens = ConversationMemoryRetriever.Tokenize("the cat is a good pet");
        Assert.DoesNotContain("the", tokens);
        Assert.DoesNotContain("is", tokens);
        Assert.DoesNotContain("a", tokens);
        Assert.Contains("cat", tokens);
        Assert.Contains("good", tokens);
        Assert.Contains("pet", tokens);
    }

    // -------------------------------------------------------------------------
    // 13. Term overlap: perfect match scores 1.0 overlap component
    // -------------------------------------------------------------------------

    [Fact]
    public void TermOverlap_PerfectMatchScoresOne()
    {
        var queryTerms = ConversationMemoryRetriever.Tokenize("machine learning neural");
        var overlap = ConversationMemoryRetriever.ComputeTermOverlap(
            "machine learning neural network", queryTerms);
        Assert.Equal(1.0, overlap, precision: 5);
    }

    [Fact]
    public void TermOverlap_NoMatchScoresZero()
    {
        var queryTerms = ConversationMemoryRetriever.Tokenize("machine learning neural");
        var overlap = ConversationMemoryRetriever.ComputeTermOverlap(
            "shopping groceries fruit vegetables", queryTerms);
        Assert.Equal(0.0, overlap, precision: 5);
    }

    // -------------------------------------------------------------------------
    // 14. Score increases with more term matches
    // -------------------------------------------------------------------------

    [Fact]
    public void Score_IncreasesWithMoreMatches()
    {
        var now = BaseNow;
        var queryTerms = ConversationMemoryRetriever.Tokenize("machine learning neural network");

        var scoreHigh = ConversationMemoryRetriever.ScoreMessage(
            "machine learning neural network deep dive", BaseNow, queryTerms, now);
        var scoreLow = ConversationMemoryRetriever.ScoreMessage(
            "machine learning", BaseNow, queryTerms, now);

        Assert.True(scoreHigh > scoreLow);
    }

    // -------------------------------------------------------------------------
    // 15. Session with only empty messages → no snippet
    // -------------------------------------------------------------------------

    [Fact]
    public void SessionWithEmptyMessages_ProducesNoSnippet()
    {
        var session = MakeSession("s1", "S1",
            new[]
            {
                new ChatMessageRecord(ChatTurnRole.User, "   ", BaseNow),
                new ChatMessageRecord(ChatTurnRole.Assistant, "", BaseNow),
            });

        var result = _retriever.Retrieve("machine learning",
            new[] { session }, topK: 1, now: BaseNow);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // 16. Multiple sessions: ordering by score then recency
    // -------------------------------------------------------------------------

    [Fact]
    public void MultipleSessionOrdering_ByScoreThenRecency()
    {
        var sessions = new[]
        {
            MakeSession("s1", "S1",
                new[] { UserMsg("python programming", BaseNow.AddDays(-10)) }),
            MakeSession("s2", "S2",
                new[] { UserMsg("python programming tutorial advanced", BaseNow.AddDays(-1)) }),
            MakeSession("s3", "S3",
                new[] { UserMsg("weather forecast", BaseNow.AddDays(-1)) }),
        };

        var result = _retriever.Retrieve(
            "python programming tutorial",
            sessions,
            topK: 3,
            now: BaseNow);

        // s2 has most overlap + is recent → first; s1 has some overlap → second; s3 no overlap → last
        Assert.Equal(3, result.Count);
        Assert.Equal("s2", result[0].SessionId);
    }
}
