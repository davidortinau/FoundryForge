namespace FoundryStudio.Core.Models;

/// <summary>
/// Which engine interprets a natural-language Discover search. User-selectable in Settings; defaults to
/// <see cref="Auto"/>. Auto resolves to the best zero-cost, zero-consent engine available at runtime
/// (Apple Intelligence → an already-cached local model → keyword matching) and NEVER silently downloads a
/// model or spends paid credits.
/// </summary>
public enum NlSearchEngine
{
    /// <summary>Pick the best free, private, already-available engine at runtime. Never auto-downloads or bills.</summary>
    Auto,

    /// <summary>Deterministic keyword matching. Always available, instant, offline, no AI.</summary>
    Keyword,

    /// <summary>On-device Apple Intelligence (Foundation Models). Free, private; macOS 26+ Apple Silicon only.</summary>
    AppleFoundationModels,

    /// <summary>In-process Foundry Local small model. Offline, private; one-time ~0.5 GB download + RAM.</summary>
    LocalModel,

    /// <summary>GitHub Copilot CLI. Highest quality but slow, uses paid credits, and sends the query online.</summary>
    CopilotCli,
}
