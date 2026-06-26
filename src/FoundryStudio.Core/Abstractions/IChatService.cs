using Microsoft.Extensions.AI;

namespace FoundryStudio.Core.Abstractions;

public interface IChatService
{
    IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
