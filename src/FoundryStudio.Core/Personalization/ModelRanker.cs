using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Personalization;

/// <summary>
/// Produces a re-ordered model list that surfaces models whose capabilities/tasks match the
/// user's <see cref="ContextProfile"/>. PURE: no IO, no state, no side-effects.
/// The result is a stable boost — every model in the input list appears in the output list,
/// just re-ordered. An empty or null profile returns the original order unchanged.
/// </summary>
public static class ModelRanker
{
    /// <summary>
    /// Returns the same model list re-ordered so that models matching the profile score higher.
    /// </summary>
    public static IReadOnlyList<ModelInfo> Rank(
        ContextProfile? profile,
        IReadOnlyList<ModelInfo> models)
    {
        if (profile is null || profile.IsEmpty || models.Count == 0)
        {
            return models;
        }

        // Build a score for each model, then stable-sort descending.
        // Scores are based on capability and task matches from the profile signals.
        var indexedModels = models.Select((m, i) => (Model: m, Index: i)).ToList();
        return indexedModels
            .Select(t => (t.Model, t.Index, Score: ComputeScore(profile, t.Model)))
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Index) // stable: preserve original order for ties
            .Select(t => t.Model)
            .ToList();
    }

    /// <summary>Returns the boost score for a single model against the profile.</summary>
    public static float ComputeScore(ContextProfile profile, ModelInfo model)
    {
        var score = 0f;

        foreach (var signal in profile.Signals)
        {
            score += signal.Domain switch
            {
                // Capability-based matches (strongest — the capability is machine-declared)
                SignalDomains.Agentic when model.Capabilities.ToolCalling =>
                    signal.Weight * 2.0f,

                SignalDomains.Reasoning when model.Capabilities.Reasoning =>
                    signal.Weight * 2.0f,

                SignalDomains.Vision when model.Capabilities.Vision =>
                    signal.Weight * 2.0f,

                // Task-based matches (weaker — metadata matching)
                SignalDomains.Coding or SignalDomains.DotNet
                    when ContainsAny(model.Task, "code", "coding", "chat") =>
                    signal.Weight * 1.0f,

                SignalDomains.Language
                    when ContainsAny(model.Task, "language", "translate", "text", "chat") =>
                    signal.Weight * 1.0f,

                // Mobile/dotnet preference — boost smaller models (lower RAM footprint)
                // only if the model actually has a reported size
                SignalDomains.Mobile or SignalDomains.DotNet
                    when model.SizeGb is > 0 and < 5.0 =>
                    signal.Weight * 0.5f,

                _ => 0f,
            };
        }

        return score;
    }

    private static bool ContainsAny(string? text, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var term in terms)
        {
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
