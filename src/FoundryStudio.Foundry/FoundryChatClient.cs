using System.Runtime.CompilerServices;
using System.Text;
using FoundryStudio.Core.Abstractions;
using FoundryStudio.Foundry.Internal;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;

namespace FoundryStudio.Foundry;

/// <summary>
/// The thin in-process Microsoft.Extensions.AI <see cref="IChatClient"/> adapter over the Foundry Local
/// SDK (FR-012, R3). Chat runs in-process — there is NO 127.0.0.1 loopback socket. Each stream takes a
/// generation lease from the <see cref="IModelStateGate"/> so a concurrent load/unload cannot tear it.
/// </summary>
public sealed class FoundryChatClient : IChatClient
{
    public const string DefaultModelAlias = "qwen2.5-0.5b";

    private readonly FoundryCatalogService _catalog;
    private readonly IModelStateGate _gate;

    public FoundryChatClient(FoundryCatalogService catalog, IModelStateGate gate)
    {
        _catalog = catalog;
        _gate = gate;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        string? modelId = null;
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            sb.Append(update.Text);
            modelId ??= update.ModelId;
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, sb.ToString())) { ModelId = modelId };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var alias = string.IsNullOrWhiteSpace(options?.ModelId) ? DefaultModelAlias : options!.ModelId!;

        // Ensure the model is loaded THROUGH the gate (a mutation), then take a generation lease so a
        // concurrent load/unload drains/rejects rather than tearing this stream (Constitution V).
        await _catalog.LoadAsync(alias, cancellationToken: cancellationToken).ConfigureAwait(false);
        var model = await _catalog.ResolveModelAsync(alias, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{alias}' not found in the Foundry Local catalog.");

        await using var lease = await _gate.BeginGenerationAsync(alias, cancellationToken).ConfigureAwait(false);
        var chatClient = await model.GetChatClientAsync(cancellationToken).ConfigureAwait(false);
        ApplyInferenceSettings(chatClient, options);
        var foundryMessages = FoundryMessageMapper.ToFoundryMessages(messages);

        int? totalTokens = null;
        int? outputTokens = null;
        string? finishReason = null;

        await foreach (var chunk in chatClient.CompleteChatStreamingAsync(foundryMessages, cancellationToken).ConfigureAwait(false))
        {
            if (FoundryMessageMapper.ExtractUsage(chunk) is { } usage)
            {
                totalTokens = usage.Total ?? totalTokens;
                outputTokens = usage.Output ?? outputTokens;
            }

            finishReason = FoundryMessageMapper.ExtractFinishReason(chunk) ?? finishReason;

            var token = FoundryMessageMapper.ExtractToken(chunk); // KI-007 #2: empty-Choices guard
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, token) { ModelId = model.Id };
        }

        // Honest trailing metrics frame — emit ONLY what FL actually provided. Absent usage/finish-reason
        // means the UI shows "unknown", never a fabricated total (FR-016, R2).
        if (totalTokens is not null || outputTokens is not null || finishReason is not null)
        {
            var trailing = new ChatResponseUpdate { Role = ChatRole.Assistant, ModelId = model.Id };
            if (totalTokens is not null || outputTokens is not null)
            {
                trailing.Contents.Add(new UsageContent(new UsageDetails
                {
                    TotalTokenCount = totalTokens,
                    OutputTokenCount = outputTokens
                }));
            }

            if (finishReason is not null)
            {
                trailing.FinishReason = MapFinishReason(finishReason);
            }

            yield return trailing;
        }
    }

    /// <summary>
    /// Apply ONLY the inference params Foundry Local actually honors WITHOUT corrupting generation:
    /// Temperature, MaxTokens, TopP. We deliberately never set <c>TopK</c>/<c>RandomSeed</c>/
    /// <c>PresencePenalty</c> (FL ignores them) — and crucially never set <c>FrequencyPenalty</c>:
    /// hardware testing showed that setting it AT ALL (even to 0) makes FL emit degenerate output
    /// (endless '.'/'?' repetition). Surfacing or sending it would be a broken control (Constitution III).
    /// </summary>
    private static void ApplyInferenceSettings(OpenAIChatClient chatClient, ChatOptions? options)
    {
        if (options is null)
        {
            return;
        }

        var settings = chatClient.Settings;
        if (options.Temperature is { } t)
        {
            settings.Temperature = t;
        }

        if (options.MaxOutputTokens is { } max)
        {
            settings.MaxTokens = max;
        }

        if (options.TopP is { } topP)
        {
            settings.TopP = topP;
        }
    }

    private static ChatFinishReason MapFinishReason(string raw) => raw.ToLowerInvariant() switch
    {
        "stop" => ChatFinishReason.Stop,
        "length" => ChatFinishReason.Length,
        "tool_calls" => ChatFinishReason.ToolCalls,
        "content_filter" => ChatFinishReason.ContentFilter,
        _ => new ChatFinishReason(raw)
    };

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }
}
