using FoundryStudio.Core.Abstractions;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace FoundryStudio.Foundry;

/// <summary>
/// The single Foundry Local lifecycle. One <see cref="FoundryLocalManager"/> backs every consumer
/// (UI in-process path and the future exposed server).
///
/// Design rules (binding):
/// - KI-005: heavy native init runs OFF the BlazorWebView dispatcher (<c>Task.Run</c>); the gate is
///   awaited, never blocked. Initialization is awaited asynchronously; the UI thread is never blocked on the init task.
/// - KI-007 #4: a faulted/canceled init is NOT memoized forever — the next <see cref="ReadyAsync"/>
///   retries instead of returning a permanently dead task.
/// </summary>
public sealed class FoundryLifecycle : IFoundryLifecycle
{
    private readonly ILogger<FoundryLifecycle> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Task<FoundryLocalManager>? _initTask;
    private volatile FoundryReadyState _state = FoundryReadyState.Uninitialized;
    private bool _disposed;

    public FoundryLifecycle(ILogger<FoundryLifecycle> logger) => _logger = logger;

    public FoundryReadyState State => _state;

    /// <inheritdoc />
    public async Task ReadyAsync(CancellationToken cancellationToken = default)
        => await GetManagerTypedAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<object> GetManagerAsync(CancellationToken cancellationToken = default)
        => await GetManagerTypedAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Strongly-typed manager access for Foundry-internal services (catalog, chat).</summary>
    public async Task<FoundryLocalManager> GetManagerTypedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Task<FoundryLocalManager> task;
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Retry on a prior faulted/canceled attempt (KI-007 #4); otherwise reuse the in-flight/ready task.
            if (_initTask is null || _initTask.IsFaulted || _initTask.IsCanceled)
            {
                _state = FoundryReadyState.Initializing;
                _initTask = Task.Run(InitializeAsync); // KI-005: off the UI dispatcher
            }

            task = _initTask;
        }
        finally
        {
            _initLock.Release();
        }

        // Awaited outside the lock; per-caller cancellation does not disturb the shared init task.
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<FoundryLocalManager> InitializeAsync()
    {
        try
        {
            var config = new Configuration
            {
                AppName = "foundrystudio",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
            };

            if (!FoundryLocalManager.IsInitialized)
            {
                await FoundryLocalManager.CreateAsync(config, _logger).ConfigureAwait(false);
            }

            var manager = FoundryLocalManager.Instance;

            var eps = manager.DiscoverEps();
            if (eps.Length > 0)
            {
                await manager.DownloadAndRegisterEpsAsync(
                    (name, percent) => _logger.LogInformation("Foundry Local EP {Name}: {Percent:F1}%", name, percent))
                    .ConfigureAwait(false);
            }

            _state = FoundryReadyState.Ready;
            _logger.LogInformation("Foundry Local is ready.");
            return manager;
        }
        catch (Exception ex)
        {
            // Honest failure: the gate stays unsatisfied and the next caller retries (faulted task is not reused).
            _state = FoundryReadyState.Failed;
            _logger.LogError(ex, "Foundry Local initialization failed: {Message}", ex.Message);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // T020: never block on an in-flight init. Only tear down a manager that initialized successfully.
        var task = _initTask;
        if (task is { IsCompletedSuccessfully: true })
        {
            object manager = await task.ConfigureAwait(false); // already completed — returns immediately, no blocking
            try
            {
                if (manager is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (manager is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Foundry Local manager on shutdown.");
            }
        }

        _initLock.Dispose();
    }
}
