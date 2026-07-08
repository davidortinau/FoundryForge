using FoundryForge.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FoundryForge.Foundry;

/// <summary>
/// The real exposed local OpenAI-compatible server (M5, the v1 "wow"). The ONLY new FL-bound class — it
/// drives the single shared <c>FoundryLocalManager</c> (via <see cref="FoundryLifecycle"/>). Start/stop are
/// serialized on a dedicated <see cref="IModelStateGate"/> scope (<see cref="ServerScopeKey"/>) so concurrent
/// server toggles (rapid on/off) can't race each other; Foundry Local coordinates web-service vs. model
/// load/unload on the shared native manager itself (verified: the server runs while models load/unload). No
/// second manager; no <c>.Result</c>/<c>.Wait()</c> (KI-005). The server is for EXTERNAL tools only — in-app
/// chat is in-process and unaffected.
/// </summary>
public sealed class LocalServerService : ILocalServerService
{
    // A stable gate scope so concurrent server toggles serialize against each other.
    private const string ServerScopeKey = "__server__";

    private readonly FoundryLifecycle _lifecycle;
    private readonly IModelStateGate _gate;
    private readonly ILogger<LocalServerService> _logger;

    private IReadOnlyList<string> _urls = Array.Empty<string>();

    public LocalServerService(FoundryLifecycle lifecycle, IModelStateGate gate, ILogger<LocalServerService> logger)
    {
        _lifecycle = lifecycle;
        _gate = gate;
        _logger = logger;
    }

    // FL's web service is available on this macOS / Apple-Silicon head. Real capability, not a faked true:
    // it is gated behind the same lifecycle that owns the native manager.
    public bool IsSupported => OperatingSystem.IsMacOS();

    public IReadOnlyList<string> Urls => _urls;

    public async Task<IReadOnlyList<string>> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            throw new NotSupportedException("The exposed local server is not supported on this platform.");
        }

        await _lifecycle.ReadyAsync(cancellationToken).ConfigureAwait(false);
        var manager = await _lifecycle.GetManagerTypedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.MutateAsync(ServerScopeKey, MutationPolicy.Drain, async () =>
        {
            await manager.StartWebServiceAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        // Read back the ACTUAL bound address(es). Empty => the start did not produce a live endpoint;
        // RETURN the empty list (don't throw) so the UI honestly shows "no endpoint" + an error, never a
        // fabricated URL (FR-006, R1). The caller distinguishes empty == failed start.
        var urls = manager.Urls ?? Array.Empty<string>();
        _urls = urls.Where(u => !string.IsNullOrWhiteSpace(u)).ToArray();

        if (_urls.Count == 0)
        {
            _logger.LogWarning("StartWebServiceAsync returned no bound URLs; reporting an empty (failed) start.");
            return _urls;
        }

        _logger.LogInformation("Local server started at {Urls}.", string.Join(", ", _urls));
        return _urls;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            return;
        }

        await _lifecycle.ReadyAsync(cancellationToken).ConfigureAwait(false);
        var manager = await _lifecycle.GetManagerTypedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.MutateAsync(ServerScopeKey, MutationPolicy.Drain, async () =>
        {
            await manager.StopWebServiceAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        _urls = Array.Empty<string>(); // server is down; no live endpoint
        _logger.LogInformation("Local server stopped.");
    }
}
