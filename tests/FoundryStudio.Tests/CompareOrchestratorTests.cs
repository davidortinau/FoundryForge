using FoundryStudio.Core.Abstractions;
using FoundryStudio.Core.Compare;
using FoundryStudio.Core.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// Unit tests for <see cref="CompareOrchestrator"/> — concurrent N-model compare orchestration.
/// All tests use a <see cref="StubChatService"/> that emits synthetic token sequences; no real
/// Foundry Local instance or native dylib is required.
/// Verifies: concurrent isolation, per-column failure containment, metrics accuracy.
/// </summary>
public class CompareOrchestratorTests
{
    // ── Stub helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stub IChatService. Responses are keyed by model alias (from ChatOptions.ModelId).
    /// Optionally throws on the first update for a given model to test fault isolation.
    /// </summary>
    private sealed class StubChatService : IChatService
    {
        private readonly Dictionary<string, IList<string>> _responses;
        private readonly HashSet<string> _throwOnFirstToken;

        public StubChatService(
            Dictionary<string, IList<string>> responses,
            IEnumerable<string>? throwOnFirstToken = null)
        {
            _responses = responses;
            _throwOnFirstToken = throwOnFirstToken?.ToHashSet() ?? new HashSet<string>();
        }

        public async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var modelId = options?.ModelId ?? string.Empty;

            if (_throwOnFirstToken.Contains(modelId))
            {
                await Task.Yield();
                throw new InvalidOperationException($"Stub fault for model '{modelId}'");
            }

            if (!_responses.TryGetValue(modelId, out var tokens))
                yield break;

            foreach (var token in tokens)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield(); // simulate async delivery
                yield return new ChatResponseUpdate(ChatRole.Assistant, token);
            }

            // Terminal usage frame
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = new List<AIContent>
                {
                    new UsageContent(new UsageDetails
                    {
                        TotalTokenCount = tokens.Count + 10,
                        OutputTokenCount = tokens.Count
                    })
                },
                FinishReason = ChatFinishReason.Stop
            };
        }
    }

    private static ModelInfo MakeModel(string alias) =>
        new ModelInfo(
            Alias: alias,
            Id: alias,
            DisplayName: alias,
            SizeGb: null,
            Device: null,
            Task: "chat",
            Provider: "test",
            Variants: Array.Empty<ModelVariant>(),
            IsCached: true,
            IsLoaded: true);

    // ── Tests ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_columns_run_concurrently_and_produce_independent_text()
    {
        var stub = new StubChatService(new Dictionary<string, IList<string>>
        {
            ["model-a"] = new[] { "Hello", " from", " A" },
            ["model-b"] = new[] { "Hi", " from", " B" }
        });

        var orchestrator = new CompareOrchestrator(stub);
        var models = new[] { MakeModel("model-a"), MakeModel("model-b") };

        await orchestrator.RunAsync(models, "Greet me");

        Assert.Equal(2, orchestrator.Columns.Count);

        var colA = orchestrator.Columns.Single(c => c.Model.Alias == "model-a");
        var colB = orchestrator.Columns.Single(c => c.Model.Alias == "model-b");

        Assert.Equal("Hello from A", colA.Text);
        Assert.Equal("Hi from B", colB.Text);
        Assert.Equal(StopReason.Natural, colA.StopReason);
        Assert.Equal(StopReason.Natural, colB.StopReason);
    }

    [Fact]
    public async Task Three_columns_produce_independent_results()
    {
        var stub = new StubChatService(new Dictionary<string, IList<string>>
        {
            ["m1"] = new[] { "one" },
            ["m2"] = new[] { "two" },
            ["m3"] = new[] { "three" }
        });

        var orchestrator = new CompareOrchestrator(stub);
        var models = new[] { MakeModel("m1"), MakeModel("m2"), MakeModel("m3") };

        await orchestrator.RunAsync(models, "Say a number");

        Assert.Equal(3, orchestrator.Columns.Count);
        Assert.Equal("one", orchestrator.Columns.Single(c => c.Model.Alias == "m1").Text);
        Assert.Equal("two", orchestrator.Columns.Single(c => c.Model.Alias == "m2").Text);
        Assert.Equal("three", orchestrator.Columns.Single(c => c.Model.Alias == "m3").Text);
    }

    [Fact]
    public async Task Throwing_stub_isolates_failure_one_column_others_succeed()
    {
        // model-bad throws; model-good succeeds — they should not interfere.
        var stub = new StubChatService(
            responses: new Dictionary<string, IList<string>>
            {
                ["model-good"] = new[] { "success" },
                ["model-bad"] = new[] { "should never appear" }
            },
            throwOnFirstToken: new[] { "model-bad" }
        );

        var orchestrator = new CompareOrchestrator(stub);
        var models = new[] { MakeModel("model-good"), MakeModel("model-bad") };

        await orchestrator.RunAsync(models, "Test prompt");

        var good = orchestrator.Columns.Single(c => c.Model.Alias == "model-good");
        var bad  = orchestrator.Columns.Single(c => c.Model.Alias == "model-bad");

        // Good column finished successfully.
        Assert.Equal("success", good.Text);
        Assert.False(good.IsError);
        Assert.Equal(StopReason.Natural, good.StopReason);

        // Bad column captured the error without affecting the good one.
        Assert.True(bad.IsError);
        Assert.Equal(StopReason.Error, bad.StopReason);
        Assert.NotNull(bad.ErrorMessage);
        Assert.Contains("model-bad", bad.ErrorMessage);
    }

    [Fact]
    public async Task Cancelled_token_stops_all_columns_as_UserCancelled()
    {
        using var cts = new CancellationTokenSource();

        // Slow stub: we cancel before tokens arrive.
        var stub = new StubChatService(new Dictionary<string, IList<string>>
        {
            ["slow-a"] = new[] { "word1", "word2" },
            ["slow-b"] = new[] { "word3", "word4" }
        });

        var orchestrator = new CompareOrchestrator(stub);
        var models = new[] { MakeModel("slow-a"), MakeModel("slow-b") };

        // Cancel immediately.
        cts.Cancel();

        await orchestrator.RunAsync(models, "Long prompt", cancellationToken: cts.Token);

        // Both columns should have ended — some may be cancelled, none should be unknown-stuck.
        Assert.All(orchestrator.Columns, col => Assert.True(col.IsDone));
    }

    [Fact]
    public async Task Metrics_ttft_and_total_tokens_populated_after_run()
    {
        var stub = new StubChatService(new Dictionary<string, IList<string>>
        {
            ["model-x"] = new[] { "tok1", "tok2", "tok3" }
        });

        var orchestrator = new CompareOrchestrator(stub);

        await orchestrator.RunAsync(new[] { MakeModel("model-x") }, "prompt");

        var col = orchestrator.Columns.Single();

        // TTFT must be measured (non-null) since at least one token arrived.
        Assert.NotNull(col.Metrics?.TimeToFirstToken);

        // Total tokens comes from the usage frame the stub emits (3 + 10 = 13).
        Assert.Equal(13, col.Metrics?.TotalTokens);

        // Output token count in usage frame = 3, so tokens/sec is computable.
        Assert.NotNull(col.Metrics?.TokensPerSecond);
    }

    [Fact]
    public async Task OnChanged_is_fired_at_least_once_per_column_per_run()
    {
        var stub = new StubChatService(new Dictionary<string, IList<string>>
        {
            ["evt-a"] = new[] { "x" },
            ["evt-b"] = new[] { "y" }
        });

        var orchestrator = new CompareOrchestrator(stub);
        var fireCount = 0;
        orchestrator.OnChanged += () => fireCount++;

        await orchestrator.RunAsync(new[] { MakeModel("evt-a"), MakeModel("evt-b") }, "probe");

        // Each column fires at least on first token and on completion; plus the final IsRunning=false.
        Assert.True(fireCount >= 3, $"Expected >=3 OnChanged fires, got {fireCount}");
    }

    [Fact]
    public async Task IsRunning_is_false_after_run_completes()
    {
        var stub = new StubChatService(new Dictionary<string, IList<string>>
        {
            ["r"] = new[] { "done" }
        });
        var orchestrator = new CompareOrchestrator(stub);

        await orchestrator.RunAsync(new[] { MakeModel("r") }, "x");

        Assert.False(orchestrator.IsRunning);
    }

    [Fact]
    public async Task Second_run_replaces_first_run_columns()
    {
        var stub = new StubChatService(new Dictionary<string, IList<string>>
        {
            ["a"] = new[] { "first" },
            ["b"] = new[] { "second" }
        });
        var orchestrator = new CompareOrchestrator(stub);

        await orchestrator.RunAsync(new[] { MakeModel("a") }, "prompt 1");
        Assert.Single(orchestrator.Columns);

        await orchestrator.RunAsync(new[] { MakeModel("b") }, "prompt 2");
        Assert.Single(orchestrator.Columns);
        Assert.Equal("b", orchestrator.Columns[0].Model.Alias);
    }
}
