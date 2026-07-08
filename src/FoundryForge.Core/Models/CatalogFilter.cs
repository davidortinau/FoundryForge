namespace FoundryForge.Core.Models;

public sealed record CatalogFilter(
    Device? Device = null,
    string? Task = null,
    string? Provider = null,
    string? SearchText = null,
    bool CachedOnly = false);
