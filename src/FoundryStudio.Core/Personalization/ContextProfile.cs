namespace FoundryStudio.Core.Personalization;

/// <summary>
/// A user's derived domain/capability profile built from on-device ~/.copilot context.
/// Deterministic and pure — no model, no network. Each <see cref="ProfileSignal"/> is a
/// named domain with a weight (0–1) and the text evidence that triggered it.
/// </summary>
public sealed record ContextProfile(IReadOnlyList<ProfileSignal> Signals)
{
    public static readonly ContextProfile Empty = new(Array.Empty<ProfileSignal>());

    public bool IsEmpty => Signals.Count == 0;

    /// <summary>
    /// Human-readable label for the Discover banner: the top N signal domain names.
    /// </summary>
    public string SummaryLabel(int maxSignals = 5)
    {
        var top = Signals
            .OrderByDescending(s => s.Weight)
            .Take(maxSignals)
            .Select(s => s.DisplayName)
            .ToList();

        return top.Count == 0 ? string.Empty : string.Join(", ", top);
    }
}

/// <summary>
/// A single domain/capability signal extracted from the user's local context.
/// </summary>
public sealed record ProfileSignal(
    string Domain,
    string DisplayName,
    float Weight,
    string Evidence);

/// <summary>Well-known domain identifiers produced by <see cref="LocalContextProfiler"/>.</summary>
public static class SignalDomains
{
    public const string Coding = "coding";
    public const string DotNet = "dotnet";
    public const string Mobile = "mobile";
    public const string Agentic = "agentic";
    public const string Reasoning = "reasoning";
    public const string Vision = "vision";
    public const string Language = "language";
}
