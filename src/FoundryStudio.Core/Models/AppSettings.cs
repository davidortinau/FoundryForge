namespace FoundryStudio.Core.Models;

public sealed record AppSettings(
    string ModelCacheDirectory,
    string? DefaultModel,
    AppTheme Theme,
    int SchemaVersion,
    bool PersonalizedRecommendations = false,
    NlSearchEngine NlSearchEngine = NlSearchEngine.Auto,
    bool SmartSearchIntroSeen = false);

public enum AppTheme
{
    Light,
    Dark,
    Auto,
}
