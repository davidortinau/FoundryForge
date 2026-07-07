using System.Runtime.Versioning;
using FoundryStudio.Core.Abstractions;
using FoundryStudio.Core.Catalog;
using FoundryStudio.Core.Models;
using FoundryStudio.Foundry;
using Microsoft.Extensions.AI;
using Microsoft.Maui.Essentials.AI;

namespace FoundryStudio.App.Services.SmartSearch;

public enum NlEngineStatus
{
    Available,
    NeedsDownload,
    NeedsSignIn,
    Unsupported,
}

/// <summary>Runtime availability of one Smart Search engine, for the Settings UI and Auto resolution.</summary>
public sealed record NlEngineAvailability(NlSearchEngine Engine, NlEngineStatus Status, string? Reason)
{
    public bool IsUsableNow => Status == NlEngineStatus.Available;
}

/// <summary>
/// Chooses the active <see cref="INlQueryInterpreter"/> from the user's setting + live engine availability,
/// and reports per-engine availability for Settings. Auto resolves to the best zero-cost, zero-consent
/// engine (Apple Intelligence → an already-cached local model → keyword) and never auto-downloads or bills.
/// </summary>
public sealed class NlEngineResolver
{
    // The small on-device model used for the LocalModel engine. CPU variant → no GPU contention with chat.
    public const string LocalModelAlias = "qwen3-0.6b";
    private const double SmallModelMaxGb = 2.0;

    private readonly FoundryChatClient _foundryChat;
    private readonly IFoundryCatalogService _catalog;
    private readonly ISettingsService _settings;
    private readonly DeterministicNlInterpreter _deterministic = new();

    public NlEngineResolver(FoundryChatClient foundryChat, IFoundryCatalogService catalog, ISettingsService settings)
    {
        _foundryChat = foundryChat;
        _catalog = catalog;
        _settings = settings;
    }

    /// <summary>The interpreter to use right now, honoring the setting and falling back safely.</summary>
    public async Task<INlQueryInterpreter> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var engine = settings.NlSearchEngine;

        if (engine == NlSearchEngine.Auto)
        {
            engine = await ResolveAutoAsync(cancellationToken).ConfigureAwait(false);
        }

        return engine switch
        {
            NlSearchEngine.AppleFoundationModels when AppleAvailable() => BuildApple(),
            NlSearchEngine.CopilotCli when CopilotAvailable() => BuildCopilot(),
            NlSearchEngine.LocalModel => await BuildLocalOrFallbackAsync(cancellationToken).ConfigureAwait(false),
            _ => _deterministic,
        };
    }

    /// <summary>The engine Auto currently resolves to (for the "Using: X" readout in Settings).</summary>
    public async Task<NlSearchEngine> ResolveAutoAsync(CancellationToken cancellationToken = default)
    {
        if (AppleAvailable())
        {
            return NlSearchEngine.AppleFoundationModels;
        }

        if (await SmallCachedAliasAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            return NlSearchEngine.LocalModel;
        }

        return NlSearchEngine.Keyword;
    }

    /// <summary>Availability of every engine, for the Settings radio list.</summary>
    public async Task<IReadOnlyList<NlEngineAvailability>> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var localAlias = await SmallCachedAliasAsync(cancellationToken).ConfigureAwait(false);

        return new[]
        {
            new NlEngineAvailability(NlSearchEngine.Keyword, NlEngineStatus.Available, null),
            AppleAvailable()
                ? new NlEngineAvailability(NlSearchEngine.AppleFoundationModels, NlEngineStatus.Available, null)
                : new NlEngineAvailability(NlSearchEngine.AppleFoundationModels, NlEngineStatus.Unsupported,
                    "Requires macOS 26 or later on Apple Silicon with Apple Intelligence turned on."),
            localAlias is not null
                ? new NlEngineAvailability(NlSearchEngine.LocalModel, NlEngineStatus.Available, null)
                : new NlEngineAvailability(NlSearchEngine.LocalModel, NlEngineStatus.NeedsDownload,
                    "Downloads a ~0.5 GB model on first use."),
            CopilotAvailable()
                ? new NlEngineAvailability(NlSearchEngine.CopilotCli, NlEngineStatus.Available, null)
                : new NlEngineAvailability(NlSearchEngine.CopilotCli, NlEngineStatus.NeedsSignIn,
                    "Install the GitHub Copilot CLI and sign in."),
        };
    }

    private static bool AppleAvailable() => OperatingSystem.IsMacOSVersionAtLeast(26);

    private static bool CopilotAvailable() => FindOnPath("copilot") is not null;

    [SupportedOSPlatform("macos26.0")]
    private INlQueryInterpreter BuildApple() =>
        new ChatModelNlInterpreter(new AppleIntelligenceChatClient(), _deterministic, TimeSpan.FromSeconds(10));

    private INlQueryInterpreter BuildCopilot()
    {
        var exe = FindOnPath("copilot") ?? "copilot";
        return new ChatModelNlInterpreter(new CopilotCliChatClient(exe), _deterministic, TimeSpan.FromSeconds(30));
    }

    private async Task<INlQueryInterpreter> BuildLocalOrFallbackAsync(CancellationToken cancellationToken)
    {
        var alias = await SmallCachedAliasAsync(cancellationToken).ConfigureAwait(false);
        if (alias is null)
        {
            // Not cached and we must not auto-download here — degrade to keyword until the user consents.
            return _deterministic;
        }

        return new ChatModelNlInterpreter(_foundryChat, _deterministic, TimeSpan.FromSeconds(20), alias);
    }

    // Smallest cached model at or under the small-model threshold, preferring the canonical alias.
    private async Task<string?> SmallCachedAliasAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cached = await _catalog.ListCachedAsync(cancellationToken).ConfigureAwait(false);
            var preferred = cached.FirstOrDefault(m => m.Alias.Contains(LocalModelAlias, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred.Alias;
            }

            return cached
                .Where(m => m.SizeGb is > 0 and <= SmallModelMaxGb)
                .OrderBy(m => m.SizeGb ?? double.MaxValue)
                .FirstOrDefault()?.Alias;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindOnPath(string executable)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }

        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
