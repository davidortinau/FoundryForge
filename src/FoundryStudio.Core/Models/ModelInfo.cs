namespace FoundryStudio.Core.Models;

public sealed record ModelInfo(
    string Alias,
    string Id,
    string DisplayName,
    double SizeGb,
    Device Device,
    string Task,
    string Provider,
    IReadOnlyList<ModelVariant> Variants,
    bool IsCached,
    bool IsLoaded);
