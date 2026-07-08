using FoundryForge.Core.Models;

namespace FoundryForge.Core.Catalog;

/// <summary>Runtime availability inputs for engine selection (platform-agnostic booleans).</summary>
public readonly record struct NlEngineAvailabilitySnapshot(bool AppleAvailable, bool LocalCached, bool CopilotAvailable);

/// <summary>
/// Pure Smart Search engine-selection logic, extracted from the platform-coupled resolver so it can be
/// unit-tested. Encodes the two honesty rules: Auto only ever picks a zero-cost, already-available engine
/// (Apple → cached local → keyword, never an un-cached download or Copilot), and an explicitly-chosen
/// engine that is not usable right now falls back to keyword rather than failing or auto-provisioning.
/// </summary>
public static class NlEngineSelection
{
    /// <summary>What Auto resolves to: best free, private, already-available engine. Never auto-cost.</summary>
    public static NlSearchEngine ResolveAuto(NlEngineAvailabilitySnapshot availability)
    {
        if (availability.AppleAvailable)
        {
            return NlSearchEngine.AppleFoundationModels;
        }

        if (availability.LocalCached)
        {
            return NlSearchEngine.LocalModel;
        }

        return NlSearchEngine.Keyword;
    }

    /// <summary>
    /// The engine to actually use for a given user setting + availability. Auto is resolved first; then any
    /// engine that is not usable right now degrades to keyword (Local requires an already-cached model — we
    /// never auto-download here).
    /// </summary>
    public static NlSearchEngine ResolveEffective(NlSearchEngine setting, NlEngineAvailabilitySnapshot availability)
    {
        var engine = setting == NlSearchEngine.Auto ? ResolveAuto(availability) : setting;

        return engine switch
        {
            NlSearchEngine.AppleFoundationModels => availability.AppleAvailable ? engine : NlSearchEngine.Keyword,
            NlSearchEngine.CopilotCli => availability.CopilotAvailable ? engine : NlSearchEngine.Keyword,
            NlSearchEngine.LocalModel => availability.LocalCached ? engine : NlSearchEngine.Keyword,
            _ => NlSearchEngine.Keyword,
        };
    }
}
