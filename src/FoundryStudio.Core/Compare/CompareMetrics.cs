using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Compare;

/// <summary>
/// Pure, clock-injected accumulator for per-column compare metrics.
/// Inputs are discrete method calls (no MEAI/ChatResponseUpdate dependency) so this class
/// is fully unit-testable with any synthetic (timestamp, token, usage) sequence.
/// Null fields = honest "unknown" — never fabricated (Constitution §III, FR-016).
/// </summary>
public sealed class CompareMetrics
{
    private DateTimeOffset? _sentAt;
    private DateTimeOffset? _firstTokenAt;
    private DateTimeOffset? _lastTokenAt;
    private int _textTokenCount;
    private int? _usageTotalTokens;
    private int? _usageOutputTokens;

    /// <summary>Record when the request was dispatched.</summary>
    public void OnSend(DateTimeOffset sentAt) => _sentAt = sentAt;

    /// <summary>Record the arrival of a non-empty text token chunk.</summary>
    public void OnToken(DateTimeOffset arrivedAt)
    {
        _firstTokenAt ??= arrivedAt;
        _lastTokenAt = arrivedAt;
        _textTokenCount++;
    }

    /// <summary>Record real usage from the terminal usage frame of the stream.</summary>
    public void OnUsage(int? totalTokens, int? outputTokens)
    {
        if (totalTokens is not null) _usageTotalTokens = totalTokens;
        if (outputTokens is not null) _usageOutputTokens = outputTokens;
    }

    /// <summary>
    /// Compute a <see cref="GenerationMetrics"/> snapshot at the current point in the stream.
    /// Returns honest nulls for values that cannot be measured yet.
    /// </summary>
    public GenerationMetrics Snapshot(StopReason stopReason)
    {
        TimeSpan? ttft = (_sentAt is { } sent && _firstTokenAt is { } first)
            ? first - sent
            : null;

        // Prefer the engine's real output-token count; fall back to segment count as an estimate.
        int? rateTokens = _usageOutputTokens ?? (_textTokenCount > 0 ? _textTokenCount : (int?)null);
        double? tokensPerSecond = null;
        if (rateTokens is { } tokens && _firstTokenAt is { } start && _lastTokenAt is { } end)
        {
            var elapsed = (end - start).TotalSeconds;
            tokensPerSecond = elapsed > 0 ? tokens / elapsed : null;
        }

        return new GenerationMetrics(
            TimeToFirstToken: ttft,
            TokensPerSecond: tokensPerSecond,
            TotalTokens: _usageTotalTokens,
            StopReason: stopReason);
    }
}
