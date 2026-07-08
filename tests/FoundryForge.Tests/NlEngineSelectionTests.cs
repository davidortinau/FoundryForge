using FoundryForge.Core.Catalog;
using FoundryForge.Core.Models;
using Xunit;

namespace FoundryForge.Tests;

/// <summary>
/// Unit tests for the pure Smart Search engine-selection logic (the testable core of NlEngineResolver).
/// Encodes the honesty rules: Auto never picks a cost-bearing engine, and unusable explicit choices
/// degrade to keyword.
/// </summary>
public class NlEngineSelectionTests
{
    private static NlEngineAvailabilitySnapshot Avail(bool apple = false, bool local = false, bool copilot = false) =>
        new(apple, local, copilot);

    // ── Auto resolution ───────────────────────────────────────────────────────────

    [Fact]
    public void Auto_prefers_apple_when_available()
    {
        var e = NlEngineSelection.ResolveAuto(Avail(apple: true, local: true, copilot: true));
        Assert.Equal(NlSearchEngine.AppleFoundationModels, e);
    }

    [Fact]
    public void Auto_uses_cached_local_when_apple_absent()
    {
        var e = NlEngineSelection.ResolveAuto(Avail(apple: false, local: true));
        Assert.Equal(NlSearchEngine.LocalModel, e);
    }

    [Fact]
    public void Auto_falls_back_to_keyword_when_nothing_free_available()
    {
        var e = NlEngineSelection.ResolveAuto(Avail(apple: false, local: false, copilot: true));
        Assert.Equal(NlSearchEngine.Keyword, e);
    }

    [Fact]
    public void Auto_never_picks_copilot_even_if_it_is_the_only_ai()
    {
        var e = NlEngineSelection.ResolveAuto(Avail(copilot: true));
        Assert.NotEqual(NlSearchEngine.CopilotCli, e);
    }

    [Fact]
    public void Auto_never_picks_uncached_local()
    {
        var e = NlEngineSelection.ResolveAuto(Avail(local: false));
        Assert.NotEqual(NlSearchEngine.LocalModel, e);
    }

    // ── Effective (explicit + fallback) ───────────────────────────────────────────

    [Fact]
    public void Explicit_keyword_always_resolves_to_keyword()
    {
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.Keyword, Avail(apple: true));
        Assert.Equal(NlSearchEngine.Keyword, e);
    }

    [Fact]
    public void Explicit_apple_used_when_available()
    {
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.AppleFoundationModels, Avail(apple: true));
        Assert.Equal(NlSearchEngine.AppleFoundationModels, e);
    }

    [Fact]
    public void Explicit_apple_falls_back_to_keyword_when_unavailable()
    {
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.AppleFoundationModels, Avail(apple: false));
        Assert.Equal(NlSearchEngine.Keyword, e);
    }

    [Fact]
    public void Explicit_local_falls_back_to_keyword_when_not_cached()
    {
        // Never auto-download: an explicit Local choice with no cached model degrades to keyword.
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.LocalModel, Avail(local: false));
        Assert.Equal(NlSearchEngine.Keyword, e);
    }

    [Fact]
    public void Explicit_local_used_when_cached()
    {
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.LocalModel, Avail(local: true));
        Assert.Equal(NlSearchEngine.LocalModel, e);
    }

    [Fact]
    public void Explicit_copilot_used_when_available()
    {
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.CopilotCli, Avail(copilot: true));
        Assert.Equal(NlSearchEngine.CopilotCli, e);
    }

    [Fact]
    public void Explicit_copilot_falls_back_when_unavailable()
    {
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.CopilotCli, Avail(copilot: false));
        Assert.Equal(NlSearchEngine.Keyword, e);
    }

    [Fact]
    public void Auto_setting_resolves_through_to_effective_engine()
    {
        // Auto + only-copilot-available → keyword (Auto never picks Copilot), even via ResolveEffective.
        var e = NlEngineSelection.ResolveEffective(NlSearchEngine.Auto, Avail(copilot: true));
        Assert.Equal(NlSearchEngine.Keyword, e);
    }
}
