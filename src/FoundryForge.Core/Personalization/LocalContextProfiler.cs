namespace FoundryForge.Core.Personalization;

/// <summary>
/// Derives a <see cref="ContextProfile"/> from already-read text inputs.
/// PURE: no file IO, no MAUI, no model. Accepts the contents of
/// <c>~/.copilot/copilot-instructions.md</c> (optional) and a list of
/// <c>(skillName, skillDescription)</c> pairs from <c>~/.copilot/skills/*/SKILL.md</c>.
/// Fully unit-testable with inline fixtures.
/// </summary>
public static class LocalContextProfiler
{
    // ── Keyword tables ─────────────────────────────────────────────────────────
    // Each table maps a domain to the keywords that vote for it.
    // Matches are case-insensitive substring/whole-word checks.

    private static readonly (string Domain, string DisplayName, string[] Keywords)[] DomainKeywords =
    [
        (SignalDomains.DotNet,  ".NET / C#",   ["dotnet", "c#", "csharp", ".net", "asp.net", "aspnet",
                                                "blazor", "maui", "nuget", "xamarin", "ef core",
                                                "entityframework", "mstest", "xunit", "nunit"]),
        (SignalDomains.Mobile,  "mobile",      ["mobile", "maui", "ios", "android", "tablet",
                                                "phone", "xamarin", "app store", "simulator",
                                                "emulator"]),
        (SignalDomains.Agentic, "agentic / tools", ["agent", "agentic", "skill", "skills", "copilot",
                                                    "automation", "workflow", "tool use", "function call",
                                                    "mcp", "orchestrat", "multi-agent"]),
        (SignalDomains.Coding,  "coding",      ["code", "coding", "programming", "develop",
                                                "development", "software", "engineer", "debug",
                                                "test", "build", "refactor", "implement",
                                                "pull request", "git"]),
        (SignalDomains.Reasoning, "reasoning", ["reason", "reasoning", "analyze", "analysis",
                                                "plan", "planning", "structured", "problem solving",
                                                "complex", "think", "inference"]),
        (SignalDomains.Vision,  "vision",      ["vision", "image", "photo", "screenshot",
                                                "visual", "picture", "ocr", "diagram"]),
        (SignalDomains.Language,"language",    ["language", "translate", "translation", "summarize",
                                                "summary", "document", "write", "writing", "grammar",
                                                "text generation", "localization"]),
    ];

    // Skill-name prefix → domain map (fast path before keyword scan)
    private static readonly (string Prefix, string[] Domains)[] SkillNamePrefixes =
    [
        ("maui",     [SignalDomains.DotNet, SignalDomains.Mobile]),
        ("dotnet",   [SignalDomains.DotNet, SignalDomains.Coding]),
        ("aspire",   [SignalDomains.DotNet, SignalDomains.Coding]),
        ("csharp",   [SignalDomains.DotNet, SignalDomains.Coding]),
        ("telemetry",[SignalDomains.Coding]),
        ("kusto",    [SignalDomains.Coding]),
        ("blog",     [SignalDomains.Language]),
        ("pdf",      [SignalDomains.Language]),
        ("xlsx",     [SignalDomains.Coding]),
        ("pptx",     [SignalDomains.Language]),
        ("skill",    [SignalDomains.Agentic]),
        ("agent",    [SignalDomains.Agentic]),
        ("fleet",    [SignalDomains.Agentic]),
        ("android",  [SignalDomains.Mobile]),
        ("ios",      [SignalDomains.Mobile]),
    ];

    /// <summary>
    /// Derives a <see cref="ContextProfile"/> from text inputs.
    /// </summary>
    /// <param name="instructionsContent">
    /// Contents of <c>~/.copilot/copilot-instructions.md</c>, or null/empty if absent.
    /// </param>
    /// <param name="skills">
    /// List of <c>(name, description)</c> pairs from <c>~/.copilot/skills/*/SKILL.md</c>.
    /// Empty list is valid.
    /// </param>
    public static ContextProfile Derive(
        string? instructionsContent,
        IReadOnlyList<(string Name, string Description)>? skills)
    {
        // Accumulate votes per domain.  Each unique evidence source adds one vote.
        var votes = new Dictionary<string, (int Count, string DisplayName, List<string> Evidences)>(
            StringComparer.OrdinalIgnoreCase);

        // ── Pass 1: instructions text ──────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(instructionsContent))
        {
            var lower = instructionsContent.ToLowerInvariant();
            foreach (var (domain, displayName, keywords) in DomainKeywords)
            {
                foreach (var kw in keywords)
                {
                    if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        Vote(votes, domain, displayName, $"instructions mention \"{kw}\"");
                        break; // one vote per keyword-group per source document
                    }
                }
            }
        }

        // ── Pass 2: skill names and descriptions ───────────────────────────────
        if (skills is { Count: > 0 })
        {
            foreach (var (skillName, skillDesc) in skills)
            {
                // Fast-path: prefix match gives strong domain signal
                var nameLower = skillName.ToLowerInvariant();
                foreach (var (prefix, domains) in SkillNamePrefixes)
                {
                    if (nameLower.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var domain in domains)
                        {
                            var displayName = GetDisplayName(domain);
                            Vote(votes, domain, displayName, $"skill \"{skillName}\"");
                        }
                    }
                }

                // Keyword scan on description
                if (!string.IsNullOrWhiteSpace(skillDesc))
                {
                    foreach (var (domain, displayName, keywords) in DomainKeywords)
                    {
                        foreach (var kw in keywords)
                        {
                            if (skillDesc.Contains(kw, StringComparison.OrdinalIgnoreCase))
                            {
                                Vote(votes, domain, displayName, $"skill \"{skillName}\" description");
                                break;
                            }
                        }
                    }
                }
            }
        }

        if (votes.Count == 0)
        {
            return ContextProfile.Empty;
        }

        // ── Normalize weights: max vote count → 1.0 ───────────────────────────
        var maxCount = (float)votes.Values.Max(v => v.Count);
        var signals = votes
            .Select(kv =>
            {
                var (count, displayName, evidences) = kv.Value;
                var weight = count / maxCount;
                var evidence = string.Join("; ", evidences.Distinct().Take(3));
                return new ProfileSignal(kv.Key, displayName, weight, evidence);
            })
            .OrderByDescending(s => s.Weight)
            .ToList();

        return new ContextProfile(signals);
    }

    private static void Vote(
        Dictionary<string, (int, string, List<string>)> votes,
        string domain,
        string displayName,
        string evidence)
    {
        if (votes.TryGetValue(domain, out var current))
        {
            current.Item3.Add(evidence);
            votes[domain] = (current.Item1 + 1, current.Item2, current.Item3);
        }
        else
        {
            votes[domain] = (1, displayName, [evidence]);
        }
    }

    private static string GetDisplayName(string domain) =>
        DomainKeywords.FirstOrDefault(d => d.Domain == domain).DisplayName ?? domain;
}
