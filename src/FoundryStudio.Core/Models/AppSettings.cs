namespace FoundryStudio.Core.Models;

public sealed record AppSettings(
    string ModelCacheDirectory,
    string? DefaultModel,
    AppTheme Theme,
    int SchemaVersion,
    bool PersonalizedRecommendations = false,
    NlSearchEngine NlSearchEngine = NlSearchEngine.Auto);

public enum AppTheme
{
    Light,
    Dark,
    Auto,
}
