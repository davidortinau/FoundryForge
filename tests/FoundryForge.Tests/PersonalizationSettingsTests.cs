using FoundryForge.Core.Models;
using FoundryForge.Core.Settings;
using Xunit;

namespace FoundryForge.Tests;

/// <summary>
/// Tests that the <c>PersonalizedRecommendations</c> setting defaults to false, round-trips correctly,
/// and is backward-compatible with JSON that predates the field.
/// </summary>
public class PersonalizationSettingsTests
{
    private const string CacheDir = "/test/cache";

    [Fact]
    public void Default_settings_have_personalization_off()
    {
        var defaults = SettingsDocument.CreateDefaults(CacheDir);
        Assert.False(defaults.PersonalizedRecommendations);
    }

    [Fact]
    public void PersonalizedRecommendations_true_round_trips()
    {
        var original = new AppSettings(CacheDir, null, AppTheme.Auto,
            SettingsDocument.CurrentSchemaVersion, PersonalizedRecommendations: true);
        var json = SettingsDocument.Serialize(original);
        var restored = SettingsDocument.Deserialize(json, CacheDir);
        Assert.True(restored.PersonalizedRecommendations);
    }

    [Fact]
    public void PersonalizedRecommendations_false_round_trips()
    {
        var original = new AppSettings(CacheDir, null, AppTheme.Auto,
            SettingsDocument.CurrentSchemaVersion, PersonalizedRecommendations: false);
        var json = SettingsDocument.Serialize(original);
        var restored = SettingsDocument.Deserialize(json, CacheDir);
        Assert.False(restored.PersonalizedRecommendations);
    }

    [Fact]
    public void Legacy_json_without_personalization_field_defaults_to_false()
    {
        // JSON written before this field existed — must not break deserialization.
        const string legacyJson = """
            {
              "ModelCacheDirectory": "/test/cache",
              "DefaultModel": null,
              "Theme": "Auto",
              "SchemaVersion": 1
            }
            """;

        var settings = SettingsDocument.Deserialize(legacyJson, CacheDir);
        Assert.False(settings.PersonalizedRecommendations);
    }

    [Fact]
    public void With_expression_preserves_personalization_value()
    {
        var original = new AppSettings(CacheDir, "qwen2.5-0.5b", AppTheme.Dark, 1,
            PersonalizedRecommendations: true);
        var updated = original with { ModelCacheDirectory = "/new/cache" };
        Assert.True(updated.PersonalizedRecommendations);
        Assert.Equal("/new/cache", updated.ModelCacheDirectory);
    }

    [Fact]
    public void AppSettings_four_arg_constructor_defaults_personalization_to_false()
    {
        // Ensure pre-existing call sites (4 positional args) still work.
        var settings = new AppSettings(CacheDir, "model", AppTheme.Light, 1);
        Assert.False(settings.PersonalizedRecommendations);
    }
}
