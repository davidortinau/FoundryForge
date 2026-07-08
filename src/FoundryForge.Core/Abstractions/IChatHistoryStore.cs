using FoundryForge.Core.Models;

namespace FoundryForge.Core.Abstractions;

/// <summary>
/// Persistence for chat conversations (user data — Constitution IV). Destructive operations are
/// consent-gated: <see cref="DeleteAsync"/>/<see cref="ClearMessagesAsync"/> with
/// <c>userConfirmed:false</c> remove nothing (FR-025/026, SC-009). <see cref="SaveAsync"/> upserts the
/// full session so a conversation survives restart (FR-023/027).
/// </summary>
public interface IChatHistoryStore
{
    Task<IReadOnlyList<ChatSession>> ListAsync(CancellationToken ct = default);

    Task<ChatSession?> GetAsync(string id, CancellationToken ct = default);

    Task SaveAsync(ChatSession session, CancellationToken ct = default);

    Task DeleteAsync(string id, bool userConfirmed, CancellationToken ct = default);

    Task ClearMessagesAsync(string id, bool userConfirmed, CancellationToken ct = default);
}
