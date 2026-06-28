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
        => Assemble(session, memoryContext: null);

    /// <summary>
    /// Assembles the MEAI message list with an optional cross-session memory context prepended
    /// as a clearly-delimited system note (Phase 5, FR-mem). The memory context is only injected
    /// when <paramref name="memoryContext"/> is non-null and non-empty; nothing is added otherwise,
    /// leaving existing chat behaviour completely unchanged. What is injected is exactly what the
    /// user sees in the UI (honesty rule, Design §9).
    /// </summary>
    public static IReadOnlyList<ChatMessage> Assemble(ChatSession session, string? memoryContext)
    {
        ArgumentNullException.ThrowIfNull(session);

        var messages = new List<ChatMessage>(session.Messages.Count + 2);

        if (!string.IsNullOrWhiteSpace(session.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, session.SystemPrompt));
        }

        // Inject cross-session memory as a clearly-delimited system note so the model has context
        // but the injection is explicit, opt-in, and identical to what the user sees in the UI.
        if (!string.IsNullOrWhiteSpace(memoryContext))
        {
            messages.Add(new ChatMessage(
                ChatRole.System,
                $"Relevant notes from earlier conversations (provided by the user):\n\n{memoryContext}"));
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
