using FoundryForge.Core.Abstractions;
using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;

namespace FoundryForge.Core.Chat;

/// <summary>
/// File-backed chat history (mirrors <c>FileSettingsService</c>): one <c>&lt;id&gt;.json</c> per conversation
/// in an App-injected directory, a <see cref="SemaphoreSlim"/> IO guard, write-temp-then-replace, and
/// <b>non-destructive recovery</b> — an unparseable file is preserved as <c>.bak</c>, never silently wiped
/// (Constitution IV). Plain managed file IO ⇒ unit-testable with a temp dir.
/// </summary>
public sealed class FileChatHistoryStore : IChatHistoryStore
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileChatHistoryStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    private string PathFor(string id) => Path.Combine(_directory, id + ".json");

    public async Task<IReadOnlyList<ChatSession>> ListAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_directory))
            {
                return Array.Empty<ChatSession>();
            }

            var sessions = new List<ChatSession>();
            foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                var session = ChatHistoryDocument.Deserialize(json);
                if (session is not null)
                {
                    sessions.Add(session);
                }
                else if (!string.IsNullOrWhiteSpace(json))
                {
                    PreserveCorrupt(file); // non-destructive recovery
                }
            }

            return sessions
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatSession?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = PathFor(id);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var session = ChatHistoryDocument.Deserialize(json);
            if (session is null && !string.IsNullOrWhiteSpace(json))
            {
                PreserveCorrupt(path);
            }

            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ChatSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);
            var path = PathFor(session.Id);
            var temp = path + ".tmp";
            var json = ChatHistoryDocument.Serialize(session);
            await File.WriteAllTextAsync(temp, json, ct).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true); // atomic replace
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, bool userConfirmed, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Constitution IV: nothing is removed without explicit consent. Guard BEFORE any file mutation.
        ConsentGuard.RequireConfirmed(userConfirmed, $"Deleting conversation '{id}'");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = PathFor(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearMessagesAsync(string id, bool userConfirmed, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ConsentGuard.RequireConfirmed(userConfirmed, $"Clearing messages in conversation '{id}'");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = PathFor(id);
            if (!File.Exists(path))
            {
                return;
            }

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var session = ChatHistoryDocument.Deserialize(json);
            if (session is null)
            {
                return;
            }

            var cleared = session with
            {
                Messages = Array.Empty<ChatMessageRecord>(),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, ChatHistoryDocument.Serialize(cleared), ct).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void PreserveCorrupt(string path)
    {
        try
        {
            var bak = path + ".bak";
            File.Move(path, bak, overwrite: true);
        }
        catch (IOException)
        {
            // Best-effort preservation; never throw out of a read.
        }
    }
}
