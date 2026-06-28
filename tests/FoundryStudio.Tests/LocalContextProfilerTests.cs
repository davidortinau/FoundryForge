using FoundryStudio.Core.Models;
using FoundryStudio.Core.Personalization;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// Tests for <see cref="LocalContextProfiler"/>.
/// PURE — no file IO. Every test uses inline fixture text.
/// </summary>
public class LocalContextProfilerTests
{
    // ── Empty / null inputs ───────────────────────────────────────────────────

    [Fact]
    public void Empty_instructions_and_no_skills_yields_empty_profile()
    {
        var profile = LocalContextProfiler.Derive(null, null);
        Assert.True(profile.IsEmpty);
        Assert.Empty(profile.Signals);
    }

    [Fact]
    public void Whitespace_instructions_and_empty_skills_yields_empty_profile()
    {
        var profile = LocalContextProfiler.Derive("   \n  ", []);
        Assert.True(profile.IsEmpty);
    }

    // ── Instructions keyword extraction ──────────────────────────────────────

    [Fact]
    public void Instructions_mentioning_dotnet_produces_dotnet_signal()
    {
        var profile = LocalContextProfiler.Derive(
            "Always use dotnet and C# coding standards.", null);

        Assert.False(profile.IsEmpty);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.DotNet);
    }

    [Fact]
    public void Instructions_mentioning_maui_produces_dotnet_and_mobile_signals()
    {
        var profile = LocalContextProfiler.Derive(
            "You are a .NET MAUI developer building mobile apps.", null);

        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.DotNet);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Mobile);
    }

    [Fact]
    public void Instructions_mentioning_agent_produces_agentic_signal()
    {
        var profile = LocalContextProfiler.Derive(
            "You are an agentic workflow automation tool. Use skills and agents.", null);

        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Agentic);
    }

    [Fact]
    public void Instructions_mentioning_vision_produces_vision_signal()
    {
        var profile = LocalContextProfiler.Derive(
            "Analyze images and process screenshots for visual review.", null);

        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Vision);
    }

    [Fact]
    public void Instructions_mentioning_reasoning_produces_reasoning_signal()
    {
        var profile = LocalContextProfiler.Derive(
            "You are a structured reasoning and planning assistant.", null);

        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Reasoning);
    }

    [Fact]
    public void Instructions_keyword_matching_is_case_insensitive()
    {
        var profile = LocalContextProfiler.Derive("DOTNET MAUI BLAZOR coding.", null);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.DotNet);
    }

    // ── Skill-name prefix matching ────────────────────────────────────────────

    [Fact]
    public void Maui_prefixed_skill_produces_dotnet_and_mobile_signals()
    {
        var profile = LocalContextProfiler.Derive(null,
        [
            ("maui-animations", "Animate MAUI controls"),
        ]);

        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.DotNet);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Mobile);
    }

    [Fact]
    public void Dotnet_prefixed_skill_produces_dotnet_and_coding_signals()
    {
        var profile = LocalContextProfiler.Derive(null,
        [
            ("dotnet-blog-author", "Write .NET blog posts"),
        ]);

        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.DotNet);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Coding);
    }

    [Fact]
    public void Skill_description_keywords_are_also_scanned()
    {
        var profile = LocalContextProfiler.Derive(null,
        [
            ("my-custom-skill", "Automate code review workflows with agents"),
        ]);

        // "code" → coding, "agent" → agentic
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Coding);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Agentic);
    }

    [Fact]
    public void Multiple_maui_skills_increase_mobile_and_dotnet_weights()
    {
        var profile = LocalContextProfiler.Derive(null,
        [
            ("maui-animations", "Animate MAUI controls"),
            ("maui-navigation", "Shell navigation in MAUI"),
            ("maui-performance", "Performance tuning for MAUI apps"),
        ]);

        var mobileSignal = profile.Signals.FirstOrDefault(s => s.Domain == SignalDomains.Mobile);
        Assert.NotNull(mobileSignal);
        Assert.Equal(1.0f, mobileSignal.Weight, precision: 2); // highest weight → 1.0
    }

    [Fact]
    public void Combined_instructions_and_skills_both_contribute()
    {
        var profile = LocalContextProfiler.Derive(
            "You are a .NET MAUI developer.",
        [
            ("maui-app-icons-splash", "Manage app icons"),
            ("kusto-telemetry", "Query telemetry data"),
        ]);

        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.DotNet);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Mobile);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Coding);
    }

    // ── SummaryLabel ──────────────────────────────────────────────────────────

    [Fact]
    public void SummaryLabel_on_empty_profile_returns_empty_string()
    {
        Assert.Equal(string.Empty, ContextProfile.Empty.SummaryLabel());
    }

    [Fact]
    public void SummaryLabel_returns_top_signal_display_names()
    {
        var profile = LocalContextProfiler.Derive(
            "dotnet maui mobile blazor agent skill", null);

        var label = profile.SummaryLabel(3);
        Assert.False(string.IsNullOrWhiteSpace(label));
        // Label should not exceed maxSignals entries
        var parts = label.Split(',');
        Assert.True(parts.Length <= 3);
    }

    // ── Weight normalization ──────────────────────────────────────────────────

    [Fact]
    public void Weights_are_normalized_so_max_is_1()
    {
        var profile = LocalContextProfiler.Derive(
            "dotnet coding blazor csharp",
        [
            ("dotnet-blog", "dotnet"),
            ("dotnet-inspect", "dotnet"),
        ]);

        Assert.All(profile.Signals, s => Assert.True(s.Weight is >= 0f and <= 1.0f));
        Assert.Contains(profile.Signals, s => s.Weight == 1.0f);
    }

    // ── Profile is non-null when inputs have content ──────────────────────────

    [Fact]
    public void Rich_maui_profile_has_multiple_signals()
    {
        var profile = LocalContextProfiler.Derive(
            "You are a .NET MAUI developer. You write blazor hybrid apps for mobile devices.",
        [
            ("maui-animations", "Animate .NET MAUI controls on iOS and Android"),
            ("maui-navigation", "Shell navigation patterns for mobile MAUI"),
            ("dotnet-inspect", "Inspect .NET processes"),
            ("skill-builder", "Build and deploy Copilot CLI skills"),
            ("kusto-telemetry", "Analyze telemetry in Azure Data Explorer"),
        ]);

        Assert.True(profile.Signals.Count >= 3);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.Mobile);
        Assert.Contains(profile.Signals, s => s.Domain == SignalDomains.DotNet);
    }
}
