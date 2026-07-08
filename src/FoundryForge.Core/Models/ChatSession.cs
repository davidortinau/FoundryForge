using FoundryForge.Core.Chat;

namespace FoundryForge.Core.Models;

/// <summary>
/// A persisted, multi-turn conversation (one human-readable JSON file under <c>&lt;AppData&gt;/chats/&lt;id&gt;.json</c>,
/// R3). User data under Constitution IV: persisted, restart-surviving, and only deleted/cleared through
/// the <c>userConfirmed</c> consent gate.
/// </summary>
public sealed record ChatSession(
    string Id,
    string Title,
    string? SystemPrompt,
    InferenceParameters Parameters,
    string? ModelAlias,
    IReadOnlyList<ChatMessageRecord> Messages,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int SchemaVersion = 1)
{
    /// <summary>A fresh, empty conversation with a stable id and default params.</summary>
    public static ChatSession New(DateTimeOffset now, string? title = null) =>
        new(
            Id: Guid.NewGuid().ToString("n"),
            Title: string.IsNullOrWhiteSpace(title) ? "New chat" : title!,
            SystemPrompt: null,
            Parameters: InferenceParameters.Defaults,
            ModelAlias: null,
            Messages: Array.Empty<ChatMessageRecord>(),
            CreatedAt: now,
            UpdatedAt: now);
}
