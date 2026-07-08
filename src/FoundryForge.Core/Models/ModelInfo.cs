namespace FoundryForge.Core.Models;

/// <summary>
/// FL-free catalog model descriptor (Constitution V / DEC-004). M2 enriches the M1 stub with real Foundry
/// Local metadata. Honesty rule: fields FL does not provide are null/empty so the UI renders
/// "unknown / not provided" rather than a fabricated value (e.g. never 0 GB as a real size).
/// </summary>
public sealed record ModelInfo(
    string Alias,
    string Id,
    string DisplayName,
    double? SizeGb,
    Device? Device,
    string Task,
    string Provider,
    IReadOnlyList<ModelVariant> Variants,
    bool IsCached,
    bool IsLoaded,
    string? ExecutionProvider = null,
    int? ContextLength = null,
    int? MaxOutputTokens = null,
    string? License = null,
    string? LicenseDescription = null,
    string? Publisher = null,
    string? ModelType = null,
    ModelCapabilities Capabilities = default);
