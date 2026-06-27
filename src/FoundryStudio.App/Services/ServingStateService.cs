using FoundryStudio.Core.Abstractions;
using FoundryStudio.Core.Models;
using FoundryStudio.Core.Server;

namespace FoundryStudio.App.Services;

/// <summary>
/// Singleton serving-state service — the SINGLE SOURCE OF TRUTH for the local server's lifecycle.
/// Both Server.razor and the ServingDock observe this service; neither holds a parallel copy.
/// Server operations (Start/Stop) go through this service; the Dock is read-only.
/// </summary>
public sealed class ServingStateService : IAsyncDisposable
{
    private readonly ILocalServerService _localServer;
    private readonly IFoundryCatalogService _catalogService;

    private ServerStatus _status = ServerStatus.Stopped;
    private bool _emptyEndpointAfterStart;
    private int _loadedCount;
    private IReadOnlyList<ModelInfo> _loadedModels = Array.Empty<ModelInfo>();

    /// <summary>Fired on any state change; observers should call StateHasChanged.</summary>
    public event Action? OnChanged;

    public bool IsSupported => _localServer.IsSupported;
    public ServerStatus Status => _status;
    public bool IsRunning => _status.IsRunning;
    public bool IsBusy => _status.IsBusy;
    public IReadOnlyList<string> Urls => _status.Urls;
    public string? Message => _status.Message;
    public bool EmptyEndpointAfterStart => _emptyEndpointAfterStart;

    /// <summary>Count of currently-loaded (tempered) models.</summary>
    public int LoadedCount => _loadedCount;

    /// <summary>Currently-loaded (tempered) models — the set reachable at the endpoint right now.</summary>
    public IReadOnlyList<ModelInfo> LoadedModels => _loadedModels;

    public ServingStateService(ILocalServerService localServer, IFoundryCatalogService catalogService)
    {
        _localServer = localServer;
        _catalogService = catalogService;

        // Initialise from real server state — if URLs are bound, the server is running.
        var urls = localServer.Urls;
        _status = localServer.IsSupported && urls.Count > 0
            ? new ServerStatus(ServerState.Running, urls)
            : ServerStatus.Stopped;
    }

    /// <summary>
    /// Refresh the loaded-model count from the catalog. Call after load/unload operations
    /// and on component init so the Dock count stays accurate.
    /// </summary>
    public async Task RefreshLoadedCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var loaded = await _catalogService.ListLoadedAsync(cancellationToken);
            _loadedCount = loaded.Count;
            _loadedModels = loaded;
        }
        catch
        {
            _loadedCount = 0;
            _loadedModels = Array.Empty<ModelInfo>();
        }

        NotifyChanged();
    }

    /// <summary>
    /// Unload a model from memory (non-destructive — cache is preserved) and refresh the list.
    /// </summary>
    public async Task UnloadModelAsync(string alias, CancellationToken cancellationToken = default)
    {
        try
        {
            await _catalogService.UnloadAsync(alias, cancellationToken);
        }
        catch
        {
            // Best-effort: fall through and refresh so the list reflects reality.
        }

        await RefreshLoadedCountAsync(cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported || IsBusy) return;

        _emptyEndpointAfterStart = false;
        _status = new ServerStatus(ServerState.Starting, Array.Empty<string>());
        NotifyChanged();

        try
        {
            var urls = await _localServer.StartAsync(cancellationToken);
            if (ServerEndpoints.BaseUrl(urls) is null)
            {
                _emptyEndpointAfterStart = true;
                _status = new ServerStatus(
                    ServerState.Error,
                    Array.Empty<string>(),
                    "Foundry Local reported a start but returned no bound address.");
            }
            else
            {
                _status = new ServerStatus(ServerState.Running, urls);
                await RefreshLoadedCountAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _status = new ServerStatus(ServerState.Error, Array.Empty<string>(), ex.Message);
        }

        NotifyChanged();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning || IsBusy) return;

        _emptyEndpointAfterStart = false;
        _status = new ServerStatus(ServerState.Stopping, _status.Urls);
        NotifyChanged();

        try
        {
            await _localServer.StopAsync(cancellationToken);
            _status = ServerStatus.Stopped;
        }
        catch (Exception ex)
        {
            _status = new ServerStatus(ServerState.Error, Array.Empty<string>(), ex.Message);
        }

        NotifyChanged();
    }

    private void NotifyChanged() => OnChanged?.Invoke();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
