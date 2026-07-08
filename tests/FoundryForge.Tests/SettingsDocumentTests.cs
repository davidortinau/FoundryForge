using FoundryForge.Core.Models;
using FoundryForge.Core.Settings;
using Xunit;

namespace FoundryForge.Tests;

public class SettingsDocumentTests
{
    private const string CacheDir = "/tmp/foundryforge-cache";

    [Fact]
    public void Defaults_are_documented_and_stable()
    {
        var defaults = SettingsDocument.CreateDefaults(CacheDir);
        Assert.Equal(CacheDir, defaults.ModelCacheDirectory);
        Assert.Null(defaults.DefaultModel);
        Assert.Equal(AppTheme.Auto, defaults.Theme);
        Assert.Equal(SettingsDocument.CurrentSchemaVersion, defaults.SchemaVersion);
    }

    [Fact]
    public void Round_trip_preserves_all_fields()
    {
        var original = new AppSettings(CacheDir, "qwen2.5-0.5b", AppTheme.Dark, SettingsDocument.CurrentSchemaVersion);
        var json = SettingsDocument.Serialize(original);
        var restored = SettingsDocument.Deserialize(json, CacheDir);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Serialized_json_is_human_readable_with_named_enum()
    {
        var json = SettingsDocument.Serialize(new AppSettings(CacheDir, null, AppTheme.Dark, 1));
        Assert.Contains("\n", json);              // indented
        Assert.Contains("\"Dark\"", json);        // enum as name, not integer
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json ")]
    public void Corrupt_or_empty_input_yields_defaults(string? json)
    {
        var settings = SettingsDocument.Deserialize(json, CacheDir);
        Assert.Equal(SettingsDocument.CreateDefaults(CacheDir), settings);
    }

    [Fact]
    public void Missing_fields_are_merged_with_defaults()
    {
        var partial = "{ \"Theme\": \"Dark\" }";
        var settings = SettingsDocument.Deserialize(partial, CacheDir);
        Assert.Equal(AppTheme.Dark, settings.Theme);       // provided
        Assert.Equal(CacheDir, settings.ModelCacheDirectory); // defaulted
        Assert.Null(settings.DefaultModel);                // defaulted
    }

    [Fact]
    public void Reset_without_consent_is_a_no_op()
    {
        var current = new AppSettings(CacheDir, "qwen2.5-0.5b", AppTheme.Dark, 1);
        var result = SettingsDocument.Reset(userConfirmed: false, current, CacheDir);
        Assert.Equal(current, result); // unchanged — settings/cache never wiped without consent
    }

    [Fact]
    public void Reset_with_consent_returns_defaults()
    {
        var current = new AppSettings(CacheDir, "qwen2.5-0.5b", AppTheme.Dark, 1);
        var result = SettingsDocument.Reset(userConfirmed: true, current, CacheDir);
        Assert.Equal(SettingsDocument.CreateDefaults(CacheDir), result);
    }
}
