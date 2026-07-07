using System.Text.Json;
using System.Text.Json.Serialization;
using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Settings;

/// <summary>
/// Pure settings logic (FR-014/015, R4). Documented defaults, human-readable JSON (de)serialization,
/// merge-missing-with-defaults, and the consent guard that prevents a destructive reset without an
/// explicit user-confirmed flag. No MAUI, no FL — the <c>PreferencesSettingsService</c> adds persistence
/// and non-destructive (.bak) file recovery on top of this.
/// </summary>
public static class SettingsDocument
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppSettings CreateDefaults(string modelCacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelCacheDirectory);
        return new AppSettings(modelCacheDirectory, DefaultModel: null, AppTheme.Auto, CurrentSchemaVersion);
    }

    public static string Serialize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings, Options);
    }

    /// <summary>
    /// Parse settings JSON. Null/empty/corrupt input returns documented defaults (non-destructive — the
    /// caller is responsible for preserving the original file). Missing fields are merged with defaults.
    /// </summary>
    public static AppSettings Deserialize(string? json, string defaultModelCacheDirectory)
    {
        var defaults = CreateDefaults(defaultModelCacheDirectory);
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaults;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<SettingsDto>(json, Options);
            return dto is null ? defaults : Merge(dto, defaults);
        }
        catch (JsonException)
        {
            return defaults;
        }
    }

    /// <summary>
    /// Consent gate (FR-015): a reset to defaults is a no-op unless <paramref name="userConfirmed"/> is true.
    /// Protects the user's settings (and the multi-GB model cache the settings point at) from silent wipe.
    /// </summary>
    public static AppSettings Reset(bool userConfirmed, AppSettings current, string defaultModelCacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(current);
        return userConfirmed ? CreateDefaults(defaultModelCacheDirectory) : current;
    }

    private static AppSettings Merge(SettingsDto dto, AppSettings defaults) => new(
        string.IsNullOrWhiteSpace(dto.ModelCacheDirectory) ? defaults.ModelCacheDirectory : dto.ModelCacheDirectory!,
        dto.DefaultModel, // null is a valid "no default model selected"
        dto.Theme ?? defaults.Theme,
        dto.SchemaVersion ?? defaults.SchemaVersion,
        dto.PersonalizedRecommendations ?? defaults.PersonalizedRecommendations,
        dto.NlSearchEngine ?? defaults.NlSearchEngine,
        dto.SmartSearchIntroSeen ?? defaults.SmartSearchIntroSeen);

    private sealed record SettingsDto(
        string? ModelCacheDirectory,
        string? DefaultModel,
        AppTheme? Theme,
        int? SchemaVersion,
        bool? PersonalizedRecommendations = null,
        NlSearchEngine? NlSearchEngine = null,
        bool? SmartSearchIntroSeen = null);
}
