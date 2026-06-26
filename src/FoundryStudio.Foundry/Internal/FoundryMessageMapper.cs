using BetalgoChatMessage = Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FoundryStudio.Foundry.Internal;

/// <summary>
/// The ONLY place Foundry Local's transitive OpenAI request/response types (Betalgo.Ranul.OpenAI) are
/// referenced (KI-007 #3). Everything outside this file works in Microsoft.Extensions.AI terms, so an FL
/// dependency change is contained to this mapper instead of leaking across the chat surface.
/// </summary>
internal static class FoundryMessageMapper
{
    public static List<BetalgoChatMessage> ToFoundryMessages(IEnumerable<MeaiChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages
            .Select(m => new BetalgoChatMessage { Role = m.Role.Value, Content = m.Text ?? string.Empty })
            .ToList();
    }

    /// <summary>
    /// Extract the assistant token from a streaming chunk. KI-007 #2: OpenAI-style streams emit frames with
    /// an empty <c>Choices</c> array (terminal/usage/keep-alive); index defensively, never <c>Choices[0]</c>.
    /// </summary>
    public static string? ExtractToken(ChatCompletionCreateResponse chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Choices is not { Count: > 0 })
        {
            return null;
        }

        // FL currently populates Message per streamed chunk (hardware-verified). Fall back to the standard
        // OpenAI streaming Delta field so a future FL/Betalgo change can't silently produce an empty stream.
        return chunk.Choices[0].Message?.Content ?? chunk.Choices[0].Delta?.Content;
    }

    /// <summary>
    /// Real usage from a chunk, if the engine provided it (terminal/usage frame). Returns null when absent —
    /// the caller surfaces "unknown", never a fabricated total (FR-016, R2).
    /// </summary>
    public static (int? Total, int? Output)? ExtractUsage(ChatCompletionCreateResponse chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Usage is null)
        {
            return null;
        }

        var total = chunk.Usage.TotalTokens;
        var output = chunk.Usage.CompletionTokens;
        // Betalgo zero-fills TotalTokens on non-usage frames; treat 0 as "not provided".
        if (total <= 0 && output is null or <= 0)
        {
            return null;
        }

        return (total > 0 ? total : (int?)null, output);
    }

    /// <summary>The raw finish reason from a chunk (e.g. "stop", "length", "tool_calls"), or null if absent.</summary>
    public static string? ExtractFinishReason(ChatCompletionCreateResponse chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Choices is not { Count: > 0 })
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(chunk.Choices[0].FinishReason) ? null : chunk.Choices[0].FinishReason;
    }
}
