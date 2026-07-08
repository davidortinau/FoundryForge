using System.Text.Json;
using System.Text.Json.Serialization;
using FoundryForge.Core.Models;

namespace FoundryForge.Core.Chat;

/// <summary>
/// Human-readable, user-auditable JSON serialization for a <see cref="ChatSession"/> (mirrors
/// <c>SettingsDocument</c>; Constitution IV). Round-trip is unit-tested (SC-008). Reads are non-throwing:
/// unparseable input yields null so the store can preserve it as <c>.bak</c> rather than wipe it.
/// </summary>
public static class ChatHistoryDocument
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Serialize(ChatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return JsonSerializer.Serialize(session, Options);
    }

    /// <summary>Parse a session; returns null on any failure (no throw out of read — Constitution IV recovery).</summary>
    public static ChatSession? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var session = JsonSerializer.Deserialize<ChatSession>(json, Options);
            // A valid document must at least carry an id; otherwise treat as corrupt.
            return string.IsNullOrWhiteSpace(session?.Id) ? null : session;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
