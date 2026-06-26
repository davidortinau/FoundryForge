using FoundryStudio.Core.Abstractions;
using Microsoft.Extensions.AI;

namespace FoundryStudio.Foundry;

/// <summary>
/// <see cref="IChatService"/> over the in-process <see cref="FoundryChatClient"/>. This is the seam where
/// MEAI middleware composes; M4 will build the pipeline as
/// <c>adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()</c>. M1 wires the adapter
/// directly. Structured output stays BEST-EFFORT only — no "guaranteed JSON" surface (E4, FR-018).
/// </summary>
public sealed class ChatService : IChatService
{
    private readonly IChatClient _chatClient;

    public ChatService(FoundryChatClient adapter)
    {
        _chatClient = adapter; // M4 seam: adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()
    }

    public IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => _chatClient.GetStreamingResponseAsync(messages, options, cancellationToken);
}
