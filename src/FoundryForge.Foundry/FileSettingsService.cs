using System.Text.Json;
using FoundryForge.Core.Abstractions;
using FoundryForge.Core.Models;
using FoundryForge.Core.Settings;

namespace FoundryForge.Foundry;

/// <summary>
/// File-backed <see cref="ISettingsService"/> (FR-014/015, R4). Persists a human-readable JSON document
/// (auditable, user-editable). Updates and resets are consent-gated and never silently wipe the user's
/// settings or the multi-GB model cache they point at (Constitution IV). An unparseable file is preserved
/// as <c>.bak</c> rather than overwritten (non-destructive recovery).
///
/// Decision (M1): this lives in the FL-managed layer and takes its paths from the constructor; the App
/// supplies <c>FileSystem.AppDataDirectory</c> and the platform model-cache path, keeping MAUI out of the
/// managed layer. (The spec named "Preferences" — a plain auditable JSON file better satisfies FR-014's
/// human-editable/auditable requirement and stays testable.)
/// </summary>
public sealed class FileSettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly string _defaultModelCacheDirectory;
    private readonly SemaphoreSlim _io = new(1, 1);

    public FileSettingsService(string filePath, string defaultModelCacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultModelCacheDirectory);
        _filePath = filePath;
        _defaultModelCacheDirectory = defaultModelCacheDirectory;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return SettingsDocument.CreateDefaults(_defaultModelCacheDirectory);
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(json) && !IsParseableJson(json))
            {
                // Non-destructive recovery: keep the unreadable file as .bak, fall back to defaults.
                TryBackup(json);
                return SettingsDocument.CreateDefaults(_defaultModelCacheDirectory);
            }

            return SettingsDocument.Deserialize(json, _defaultModelCacheDirectory);
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await WriteAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetAsync(bool userConfirmed, CancellationToken cancellationToken = default)
    {
        // Consent gate: without explicit confirmation this is a no-op (settings/cache are never wiped silently).
        if (!userConfirmed)
        {
            return;
        }

        await WriteAsync(SettingsDocument.CreateDefaults(_defaultModelCacheDirectory), cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_filePath, SettingsDocument.Serialize(settings), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }
    }

    private void TryBackup(string json)
    {
        try
        {
            File.WriteAllText(_filePath + ".bak", json);
        }
        catch (IOException)
        {
            // best-effort backup; never throw out of a read
        }
    }

    private static bool IsParseableJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
