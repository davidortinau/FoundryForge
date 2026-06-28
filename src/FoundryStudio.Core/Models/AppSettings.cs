namespace FoundryStudio.Core.Models;

public sealed record AppSettings(
    string ModelCacheDirectory,
    string? DefaultModel,
    AppTheme Theme,
    int SchemaVersion,
    bool PersonalizedRecommendations = false);

public enum AppTheme
{
    Light,
    Dark,
    Auto,
}
