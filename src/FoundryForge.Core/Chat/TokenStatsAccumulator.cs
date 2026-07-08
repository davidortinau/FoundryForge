using FoundryForge.Core.Models;
using Microsoft.Extensions.AI;

namespace FoundryForge.Core.Chat;

/// <summary>
/// Deterministic, clock-injected accumulator that turns the real <see cref="ChatResponseUpdate"/> stream
/// into honest <see cref="GenerationMetrics"/> (US4, R2). Every value is measured or null — null is the
/// honest "unknown" (Constitution III, FR-016). No real clock or IO inside, so it is unit-testable over a
/// synthetic update sequence.
/// </summary>
public sealed class TokenStatsAccumulator
{
    private DateTimeOffset? _sentAt;
    private DateTimeOffset? _firstTokenAt;
    private DateTimeOffset? _lastTokenAt;
    private int _textUpdateCount;
    private int? _usageTotalTokens;
    private int? _usageOutputTokens;

    public void OnSend(DateTimeOffset sentAt) => _sentAt = sentAt;

    public void OnUpdate(ChatResponseUpdate update, DateTimeOffset arrivedAt)
    {
        ArgumentNullException.ThrowIfNull(update);

        var text = update.Text;
        if (!string.IsNullOrEmpty(text))
        {
            _firstTokenAt ??= arrivedAt;
            _lastTokenAt = arrivedAt;
            _textUpdateCount++;
        }

        // Capture real usage if the stream carries it (terminal usage frame); otherwise it stays unknown.
        foreach (var content in update.Contents)
        {
            if (content is UsageContent usage && usage.Details is { } details)
            {
                if (details.TotalTokenCount is { } total)
                {
                    _usageTotalTokens = (int)total;
                }

                if (details.OutputTokenCount is { } output)
                {
                    _usageOutputTokens = (int)output;
                }
            }
        }
    }

    public GenerationMetrics Complete(StopReason stopReason)
    {
        TimeSpan? ttft = (_sentAt is { } sent && _firstTokenAt is { } first)
            ? first - sent
            : null;

        // Prefer the engine's real output-token count over update-segment count for the rate numerator.
        int? rateTokens = _usageOutputTokens ?? (_textUpdateCount > 0 ? _textUpdateCount : (int?)null);
        double? tokensPerSecond = null;
        if (rateTokens is { } tokens && _firstTokenAt is { } start && _lastTokenAt is { } end)
        {
            var elapsed = (end - start).TotalSeconds;
            tokensPerSecond = elapsed > 0 ? tokens / elapsed : null;
        }

        return new GenerationMetrics(
            TimeToFirstToken: ttft,
            TokensPerSecond: tokensPerSecond,
            TotalTokens: _usageTotalTokens, // null = honest "unknown"
            StopReason: stopReason);
    }
}
