using FoundryStudio.Core.Models;
using Microsoft.Extensions.AI;

namespace FoundryStudio.Core.Chat;

/// <summary>
/// Builds the full multi-turn MEAI request from a persisted <see cref="ChatSession"/> (US1, FR-004/017).
/// Pure, deterministic, dylib-free.
/// </summary>
public static class TranscriptAssembler
{
    public static IReadOnlyList<ChatMessage> Assemble(ChatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var messages = new List<ChatMessage>(session.Messages.Count + 1);

        if (!string.IsNullOrWhiteSpace(session.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, session.SystemPrompt));
        }

        foreach (var record in session.Messages)
        {
            messages.Add(new ChatMessage(ToChatRole(record.Role), record.Content));
        }

        return messages;
    }

    private static ChatRole ToChatRole(ChatTurnRole role) => role switch
    {
        ChatTurnRole.System => ChatRole.System,
        ChatTurnRole.User => ChatRole.User,
        ChatTurnRole.Assistant => ChatRole.Assistant,
        ChatTurnRole.Tool => ChatRole.Tool,
        _ => ChatRole.User
    };
}
