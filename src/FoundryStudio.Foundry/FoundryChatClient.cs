using System.Runtime.CompilerServices;
using System.Text;
using FoundryStudio.Core.Abstractions;
using FoundryStudio.Foundry.Internal;
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
        var foundryMessages = FoundryMessageMapper.ToFoundryMessages(messages);

        // TODO(M4): map ChatOptions (temperature/max tokens; tools + response_format as BEST-EFFORT per M0d) into the request.
        await foreach (var chunk in chatClient.CompleteChatStreamingAsync(foundryMessages, cancellationToken).ConfigureAwait(false))
        {
            var token = FoundryMessageMapper.ExtractToken(chunk); // KI-007 #2: empty-Choices guard
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, token) { ModelId = model.Id };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }
}
