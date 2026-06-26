namespace FoundryStudio.Core.Models;

public sealed record AppSettings(
    string ModelCacheDirectory,
    string? DefaultModel,
    AppTheme Theme,
    int SchemaVersion);

public enum AppTheme
{
    Light,
    Dark,
    Auto,
}
