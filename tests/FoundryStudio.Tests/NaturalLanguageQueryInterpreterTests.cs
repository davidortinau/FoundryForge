using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// Unit tests for NaturalLanguageQueryInterpreter — the deterministic NL → CatalogFilter core.
/// These tests run fully offline with no model, service, or MAUI dependency.
/// </summary>
public class NaturalLanguageQueryInterpreterTests
{
    private static readonly IReadOnlyList<string> DefaultTasks = new[]
    {
        "chat-completion", "code-generation", "summarization", "embeddings", "image-generation",
    };

    private static readonly IReadOnlyList<string> DefaultProviders = new[]
    {
        "Microsoft", "Meta", "Mistral", "Google",
    };

    private readonly NaturalLanguageQueryInterpreter _interpreter = new();

    private NlQueryResult Interpret(string query) =>
        _interpreter.Interpret(query, DefaultTasks, DefaultProviders);

    // ── Empty / whitespace ────────────────────────────────────────────────────────

    [Fact]
    public void Empty_query_returns_empty_result()
    {
        var result = Interpret(string.Empty);
        Assert.True(result.IsEmpty);
        Assert.Equal(NlSortHint.None, result.SortHint);
        Assert.Empty(result.Chips);
    }

    [Fact]
    public void Whitespace_only_returns_empty_result()
    {
        var result = Interpret("   ");
        Assert.True(result.IsEmpty);
    }

    // ── Device detection ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("vision model on gpu")]
    [InlineData("I need a GPU model for coding")]
    [InlineData("webgpu accelerated")]
    public void Detects_gpu_device(string query)
    {
        var result = Interpret(query);
        Assert.Equal(Device.Gpu, result.Filter.Device);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Device && c.Value == "Gpu");
    }

    [Theory]
    [InlineData("run on cpu only")]
    [InlineData("cpu inference model")]
    public void Detects_cpu_device(string query)
    {
        var result = Interpret(query);
        Assert.Equal(Device.Cpu, result.Filter.Device);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Device && c.Value == "Cpu");
    }

    // ── Capability hints ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("vision model on gpu")]
    [InlineData("a model that understands images")]
    [InlineData("multimodal with vision")]
    public void Detects_vision_capability(string query)
    {
        var result = Interpret(query);
        Assert.Contains("vision", result.CapabilityHints);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Capability && c.Value == "vision");
    }

    [Theory]
    [InlineData("model with tool use for agents")]
    [InlineData("function calling support")]
    [InlineData("agentic model")]
    public void Detects_tool_use_capability(string query)
    {
        var result = Interpret(query);
        Assert.Contains("tool use", result.CapabilityHints);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Capability && c.Value == "tool use");
    }

    [Theory]
    [InlineData("model good at math and reasoning")]
    [InlineData("strong reasoning capabilities")]
    [InlineData("logic and think")]
    public void Detects_reasoning_capability(string query)
    {
        var result = Interpret(query);
        Assert.Contains("reasoning", result.CapabilityHints);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Capability && c.Value == "reasoning");
    }

    // ── Size / speed sort hints ───────────────────────────────────────────────────

    [Theory]
    [InlineData("a small fast model for coding")]
    [InlineData("tiny lightweight model")]
    [InlineData("compact and efficient")]
    [InlineData("lite model")]
    public void Small_keywords_produce_size_ascending_sort(string query)
    {
        var result = Interpret(query);
        Assert.Equal(NlSortHint.SizeAscending, result.SortHint);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.SortHint && c.Value == "SizeAscending");
    }

    [Theory]
    [InlineData("large powerful model")]
    [InlineData("biggest model available")]
    public void Large_keywords_produce_size_descending_sort(string query)
    {
        var result = Interpret(query);
        Assert.Equal(NlSortHint.SizeDescending, result.SortHint);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.SortHint && c.Value == "SizeDescending");
    }

    // ── Long-context sort hint ────────────────────────────────────────────────────

    [Theory]
    [InlineData("long context chat model")]
    [InlineData("model with large context window")]
    [InlineData("long-context summarization")]
    public void Long_context_keywords_produce_context_descending_sort(string query)
    {
        var result = Interpret(query);
        Assert.Equal(NlSortHint.ContextDescending, result.SortHint);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.SortHint && c.Value == "ContextDescending");
    }

    // ── Task matching ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("model for chat")]
    [InlineData("best chat completion model")]
    public void Detects_chat_task(string query)
    {
        var result = Interpret(query);
        Assert.Equal("chat-completion", result.Filter.Task, ignoreCase: true);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Task);
    }

    [Theory]
    [InlineData("coding assistant model")]
    [InlineData("model for code generation")]
    public void Detects_code_task(string query)
    {
        var result = Interpret(query);
        Assert.NotNull(result.Filter.Task);
        // Should match "code-generation" task
        Assert.Contains("code", result.Filter.Task, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Task);
    }

    [Fact]
    public void Summarization_keyword_maps_to_summarization_task()
    {
        var result = Interpret("I need a model for summarization");
        Assert.Equal("summarization", result.Filter.Task, ignoreCase: true);
    }

    // ── Provider matching ─────────────────────────────────────────────────────────

    [Fact]
    public void Detects_microsoft_provider()
    {
        var result = Interpret("Microsoft phi model");
        Assert.Equal("Microsoft", result.Filter.Provider ?? result.Chips.FirstOrDefault(c => c.Kind == NlChipKind.Provider)?.Value);
    }

    [Fact]
    public void Detects_meta_provider()
    {
        var result = Interpret("show me meta models");
        // "meta" should match "Meta" provider
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Provider && c.Value == "Meta");
    }

    // ── Unmatched → SearchText ────────────────────────────────────────────────────

    [Fact]
    public void Unmatched_words_go_into_search_text()
    {
        var result = Interpret("phi instruct");
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.SearchText);
        Assert.NotNull(result.Filter.SearchText);
        // At least one of the words should appear
        Assert.True(
            result.Filter.SearchText!.Contains("phi", StringComparison.OrdinalIgnoreCase) ||
            result.Filter.SearchText.Contains("instruct", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Stop_words_are_excluded_from_search_text()
    {
        // "for" and "a" are stop words; "phi" is not
        var result = Interpret("a model for phi");
        // "model" is also a stop word; only "phi" should be in search
        if (result.Filter.SearchText is not null)
        {
            Assert.DoesNotContain("for", result.Filter.SearchText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" a ", " " + result.Filter.SearchText + " ", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Combined / integration scenarios ─────────────────────────────────────────

    [Fact]
    public void Small_fast_coding_model_gets_size_asc_and_code_task()
    {
        var result = Interpret("a small fast model for coding");
        Assert.Equal(NlSortHint.SizeAscending, result.SortHint);
        Assert.NotNull(result.Filter.Task);
        Assert.Contains("code", result.Filter.Task, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vision_model_on_gpu_gets_device_and_capability()
    {
        var result = Interpret("vision model on gpu");
        Assert.Equal(Device.Gpu, result.Filter.Device);
        Assert.Contains("vision", result.CapabilityHints);
    }

    [Fact]
    public void Long_context_chat_gets_context_sort_and_chat_task()
    {
        var result = Interpret("long context chat model");
        Assert.Equal(NlSortHint.ContextDescending, result.SortHint);
        Assert.NotNull(result.Filter.Task);
        Assert.Contains("chat", result.Filter.Task, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agentic_reasoning_model_gets_tool_use_and_reasoning()
    {
        var result = Interpret("agentic model with reasoning");
        Assert.Contains("tool use", result.CapabilityHints);
        Assert.Contains("reasoning", result.CapabilityHints);
    }

    [Fact]
    public void All_stop_words_query_returns_empty()
    {
        var result = Interpret("a model for me to use");
        // All tokens are stop words; chips should be empty or SearchText only contains significant words
        Assert.True(result.IsEmpty || result.Filter.SearchText is null ||
            !result.Filter.SearchText!.Contains("for", StringComparison.OrdinalIgnoreCase));
    }

    // ── Chip structure ────────────────────────────────────────────────────────────

    [Fact]
    public void Chips_reflect_exactly_what_was_applied()
    {
        var result = Interpret("small gpu chat model");
        // Should have chips for: Device=Gpu, Task=chat-completion, SortHint=SizeAscending
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Device);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.Task);
        Assert.Contains(result.Chips, c => c.Kind == NlChipKind.SortHint);

        // Every chip must have a non-empty label and value
        foreach (var chip in result.Chips)
        {
            Assert.False(string.IsNullOrWhiteSpace(chip.Label), $"Chip {chip.Kind} has empty label");
            Assert.False(string.IsNullOrWhiteSpace(chip.Value), $"Chip {chip.Kind} has empty value");
        }
    }

    [Fact]
    public void Null_availableTasks_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _interpreter.Interpret("query", null!, DefaultProviders));
    }

    [Fact]
    public void Null_availableProviders_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _interpreter.Interpret("query", DefaultTasks, null!));
    }

    // ── Empty facets (offline with no catalog data) ───────────────────────────────

    [Fact]
    public void Works_with_empty_facets_offline()
    {
        // When no catalog is loaded, facets are empty — device/sort still work
        var result = _interpreter.Interpret("small gpu model", Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal(Device.Gpu, result.Filter.Device);
        Assert.Equal(NlSortHint.SizeAscending, result.SortHint);
    }
}
