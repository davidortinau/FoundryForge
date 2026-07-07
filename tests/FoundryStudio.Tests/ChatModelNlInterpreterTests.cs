using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// Unit tests for ChatModelNlInterpreter using a fake IChatClient — no live model. Covers JSON parsing,
/// facet validation (honesty), keyword-corpus filtering, and deterministic fallback on failure/timeout.
/// </summary>
public class ChatModelNlInterpreterTests
{
    private static readonly CatalogFacets Facets = new(
        Tasks: new[] { "chat-completion", "code-generation" },
        Providers: new[] { "Microsoft", "Meta" },
        Devices: new[] { Device.Gpu, Device.Cpu },
        SearchTokens: new[] { "phi", "qwen", "llama", "instruct", "mini" });

    private static ChatModelNlInterpreter Build(string modelReply, TimeSpan? timeout = null) =>
        new(new FakeChatClient(modelReply), new DeterministicNlInterpreter(), timeout);

    private static ChatModelNlInterpreter BuildThrowing(Exception ex) =>
        new(new FakeChatClient(ex), new DeterministicNlInterpreter());

    [Fact]
    public async Task Parses_clean_json_into_filter_and_chips()
    {
        var interp = Build("""{"device":"gpu","task":"code-generation","capabilities":["vision"],"sort":"size-asc","keywords":["qwen"]}""");

        var r = await interp.InterpretAsync("small qwen coder that sees images on gpu", Facets);

        Assert.Equal(Device.Gpu, r.Filter.Device);
        Assert.Equal("code-generation", r.Filter.Task);
        Assert.Equal("qwen", r.Filter.SearchText);
        Assert.Equal(NlSortHint.SizeAscending, r.SortHint);
        Assert.Contains("vision", r.CapabilityHints);
    }

    [Fact]
    public async Task Strips_markdown_fence_and_prose()
    {
        var interp = Build("""
            Here is the JSON:
            ```json
            {"device":"cpu","keywords":["phi"]}
            ```
            """);

        var r = await interp.InterpretAsync("phi on cpu", Facets);

        Assert.Equal(Device.Cpu, r.Filter.Device);
        Assert.Equal("phi", r.Filter.SearchText);
    }

    [Fact]
    public async Task Rejects_device_not_in_facets()
    {
        // npu is a valid enum but not present in this catalog's facets — must be dropped (honesty).
        var interp = Build("""{"device":"npu","keywords":["phi"]}""");

        var r = await interp.InterpretAsync("phi on npu", Facets);

        Assert.Null(r.Filter.Device);
        Assert.Equal("phi", r.Filter.SearchText);
    }

    [Fact]
    public async Task Drops_keywords_not_in_catalog_corpus()
    {
        // Model hallucinated filler into keywords — corpus validation drops them.
        var interp = Build("""{"capabilities":["vision"],"keywords":["inspect","them","load"]}""");

        var r = await interp.InterpretAsync("I want to load images and inspect them", Facets);

        Assert.Null(r.Filter.SearchText);
        Assert.Contains("vision", r.CapabilityHints);
    }

    [Fact]
    public async Task Normalizes_capability_synonyms()
    {
        var interp = Build("""{"capabilities":["tool use","reason"]}""");

        var r = await interp.InterpretAsync("agent that thinks", Facets);

        Assert.Contains("tools", r.CapabilityHints);
        Assert.Contains("reasoning", r.CapabilityHints);
    }

    [Fact]
    public async Task Falls_back_to_deterministic_on_bad_json()
    {
        var interp = Build("this is not json at all");

        var r = await interp.InterpretAsync("small gpu model", Facets);

        // Deterministic interpreter handles device + sort.
        Assert.Equal(Device.Gpu, r.Filter.Device);
        Assert.Equal(NlSortHint.SizeAscending, r.SortHint);
    }

    [Fact]
    public async Task Falls_back_to_deterministic_on_model_error()
    {
        var interp = BuildThrowing(new InvalidOperationException("model unavailable"));

        var r = await interp.InterpretAsync("gpu phi", Facets);

        Assert.Equal(Device.Gpu, r.Filter.Device);
    }

    [Fact]
    public async Task Falls_back_when_model_extracts_nothing()
    {
        var interp = Build("""{"device":null,"task":null,"capabilities":[],"sort":null,"keywords":[]}""");

        // Empty AI result -> deterministic pass still finds the device.
        var r = await interp.InterpretAsync("cpu model", Facets);

        Assert.Equal(Device.Cpu, r.Filter.Device);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_not_swallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var interp = Build("""{"device":"gpu"}""");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => interp.InterpretAsync("gpu", Facets, cts.Token));
    }

    [Fact]
    public async Task Drops_generic_word_keywords_that_would_overconstrain()
    {
        // The model wrongly emitted generic words as keywords; strict matching drops them so they can't
        // filter the catalog to zero. "qwen" is a real family fragment and survives.
        var interp = Build("""{"capabilities":["reasoning"],"sort":"size-asc","keywords":["small","reasoning","qwen"]}""");

        var r = await interp.InterpretAsync("a small model good at reasoning like qwen", Facets);

        Assert.Equal("qwen", r.Filter.SearchText);
        Assert.Contains("reasoning", r.CapabilityHints);
        Assert.Equal(NlSortHint.SizeAscending, r.SortHint);
    }

    // Minimal IChatClient test double: returns a fixed reply or throws.
    private sealed class FakeChatClient : IChatClient
    {
        private readonly string? _reply;
        private readonly Exception? _throw;

        public FakeChatClient(string reply) => _reply = reply;
        public FakeChatClient(Exception ex) => _throw = ex;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply ?? string.Empty)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
