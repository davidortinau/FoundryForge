using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Catalog;

/// <summary>
/// Derives an honest <see cref="ModelCapabilities"/> from FL metadata (M2, R2). Pure/deterministic, no
/// fabrication: vision from input modalities, tool-calling from the nullable flag (tracking known-ness),
/// reasoning ONLY if FL's capabilities string declares it — never inferred from a model's name.
/// </summary>
public static class CapabilityParser
{
    private static readonly string[] VisionTokens = { "image", "vision", "multimodal" };
    private static readonly string[] ReasoningTokens = { "reasoning", "reason", "thinking", "chain-of-thought", "cot" };

    public static ModelCapabilities Parse(string? capabilities, string? inputModalities, bool? supportsToolCalling)
    {
        var vision = ContainsAny(inputModalities, VisionTokens);
        var reasoning = ContainsAny(capabilities, ReasoningTokens);
        var toolCalling = supportsToolCalling == true;
        var toolCallingKnown = supportsToolCalling.HasValue;

        return new ModelCapabilities(vision, toolCalling, reasoning, toolCallingKnown);
    }

    private static bool ContainsAny(string? haystack, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (haystack.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
