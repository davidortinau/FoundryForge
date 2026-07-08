namespace FoundryForge.Core.Models;

/// <summary>
/// Why a generation stopped. Mapped from MEAI <c>ChatFinishReason</c> (Stop→Natural, Length→MaxTokens,
/// ToolCalls→ToolCalls); a user Stop (US3) becomes <see cref="UserCancelled"/>; a stream fault becomes
/// <see cref="Error"/>; an absent finish reason is honestly <see cref="Unknown"/> (FR-015).
/// </summary>
public enum StopReason
{
    Natural,
    MaxTokens,
    ToolCalls,
    UserCancelled,
    Error,
    Unknown
}

/// <summary>
/// Display-only metrics for one assistant turn, derived entirely from the real
/// <c>ChatResponseUpdate</c> stream. Each nullable value is an honest "unknown" — never fabricated or
/// back-computed-and-presented-as-exact (Constitution III, FR-016).
/// </summary>
public sealed record GenerationMetrics(
    TimeSpan? TimeToFirstToken,
    double? TokensPerSecond,
    int? TotalTokens,
    StopReason StopReason);
