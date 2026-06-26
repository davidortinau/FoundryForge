namespace FoundryStudio.Core.Models;

/// <summary>
/// An honest, always-labeled-an-estimate view of context-window usage (US6, FR-020/021/022). When
/// <see cref="ContextLength"/> is null the limit is genuinely unknown: <see cref="Fraction"/> stays null
/// and no denominator/percentage is fabricated.
/// </summary>
public sealed record ContextWindowEstimate(
    int UsedTokensEstimate,
    int? ContextLength,
    double? Fraction,
    bool IsWarn,
    bool IsUnknown);
