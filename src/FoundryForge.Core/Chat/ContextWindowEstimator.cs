using FoundryForge.Core.Models;

namespace FoundryForge.Core.Chat;

/// <summary>
/// Produces an honest, always-an-estimate context-window view (US6, R7). Never fabricates a denominator
/// or percentage when the model's context length is unknown (FR-022). Pure and deterministic.
/// </summary>
public static class ContextWindowEstimator
{
    public static ContextWindowEstimate Estimate(int usedTokensEstimate, int? contextLength, double warnFraction = 0.8)
    {
        if (usedTokensEstimate < 0)
        {
            usedTokensEstimate = 0;
        }

        if (contextLength is null || contextLength.Value <= 0)
        {
            return new ContextWindowEstimate(
                UsedTokensEstimate: usedTokensEstimate,
                ContextLength: contextLength,
                Fraction: null,
                IsWarn: false,
                IsUnknown: true);
        }

        var fraction = (double)usedTokensEstimate / contextLength.Value;
        return new ContextWindowEstimate(
            UsedTokensEstimate: usedTokensEstimate,
            ContextLength: contextLength,
            Fraction: fraction,
            IsWarn: fraction >= warnFraction,
            IsUnknown: false);
    }

    /// <summary>
    /// A coarse, explicitly-approximate token count for a body of text (~4 chars/token). Documented as an
    /// estimate; the UI must always label it so (FR-020).
    /// </summary>
    public static int ApproximateTokens(string? text)
        => string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
}
