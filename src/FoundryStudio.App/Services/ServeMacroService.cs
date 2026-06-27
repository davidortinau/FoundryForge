using FoundryStudio.Core.Abstractions;

namespace FoundryStudio.App.Services;

/// <summary>
/// Named stages in the Cast → Temper → Serve macro. Ordered so numeric comparison works
/// for "is this stage completed?" (Idle=0 < Casting=1 < Tempering=2 < Serving=3 < Done=4; Failed=5 is terminal).
/// </summary>
public enum ServeStage
{
    Idle = 0,
    Casting = 1,
    Tempering = 2,
    Serving = 3,
    Done = 4,
    Failed = 5,
}

/// <summary>Progress snapshot emitted by <see cref="ServeMacroService"/> as the macro advances.</summary>
public sealed class ServeProgress
{
    public ServeStage Stage { get; init; }

    /// <summary>Human-readable name of the stage that failed ("Cast", "Temper", or "Serve").</summary>
    public string? FailedAtStage { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>The exact model id (as used in the API "model" field) when Stage == Done.</summary>
    public string? CompletedModelId { get; init; }
}

/// <summary>
/// Orchestrates the primary-job macro: Cast (download) → Temper (load) → Serve (start server).
/// Each stage emits a <see cref="ServeProgress"/> event so the UI can animate staged progress
/// and name the stage if an error occurs (constitution honesty rule).
///
/// Register as Transient so each UI component gets its own independent instance.
/// </summary>
public sealed class ServeMacroService
{
    private readonly IFoundryCatalogService _catalog;
    private readonly ServingStateService _servingState;

    public event Action<ServeProgress>? OnProgress;

    public ServeMacroService(IFoundryCatalogService catalog, ServingStateService servingState)
    {
        _catalog = catalog;
        _servingState = servingState;
    }

    /// <summary>
    /// Run the full Cast → Temper → Serve macro (or Cast → Temper only if <paramref name="temperOnly"/> is true).
    /// All exceptions are caught and converted to <see cref="ServeStage.Failed"/> progress events;
    /// callers never need to catch.
    /// </summary>
    public async Task RunAsync(
        string alias,
        string? variantId = null,
        bool temperOnly = false,
        CancellationToken cancellationToken = default)
    {
        // ── STAGE 1: Cast ───────────────────────────────────────────────────────────────────────
        Emit(ServeStage.Casting);
        try
        {
            var model = await _catalog.GetModelAsync(alias, cancellationToken);
            if (model is null || !model.IsCached)
            {
                await _catalog.DownloadAsync(alias, progress: null, variantId, cancellationToken);
            }
            // If already cached we skip the download but still report the stage as passing through.
        }
        catch (Exception ex)
        {
            Emit(ServeStage.Failed, "Cast", ex.Message);
            return;
        }

        // ── STAGE 2: Temper ─────────────────────────────────────────────────────────────────────
        Emit(ServeStage.Tempering);
        try
        {
            await _catalog.LoadAsync(alias, variantId, cancellationToken);
            await _servingState.RefreshLoadedCountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Emit(ServeStage.Failed, "Temper", ex.Message);
            return;
        }

        if (temperOnly)
        {
            var tempered = await TryGetModelAsync(alias, cancellationToken);
            Emit(ServeStage.Done, completedModelId: tempered?.Id);
            return;
        }

        // ── STAGE 3: Serve ──────────────────────────────────────────────────────────────────────
        Emit(ServeStage.Serving);
        if (!_servingState.IsRunning)
        {
            await _servingState.StartAsync(cancellationToken);
        }

        if (!_servingState.IsRunning)
        {
            Emit(ServeStage.Failed, "Serve",
                _servingState.Message ?? "The server failed to start. Check the server status panel above.");
            return;
        }

        var loaded = await TryGetModelAsync(alias, cancellationToken);
        Emit(ServeStage.Done, completedModelId: loaded?.Id);
    }

    /// <summary>
    /// Test the live endpoint by performing a real GET /v1/models.
    /// Never throws — returns (false, message) on failure.
    /// </summary>
    public async Task<(bool Ok, string? Message)> TestConnectionAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var url = $"{baseUrl.TrimEnd('/')}/v1/models";
            var response = await client.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode
                ? (true, $"Connected — /v1/models responded with HTTP {(int)response.StatusCode}.")
                : (false, $"Server responded with HTTP {(int)response.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            return (false, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return (false, "Connection timed out after 8 s.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<Core.Models.ModelInfo?> TryGetModelAsync(string alias, CancellationToken ct)
    {
        try { return await _catalog.GetModelAsync(alias, ct); }
        catch { return null; }
    }

    private void Emit(
        ServeStage stage,
        string? failedAtStage = null,
        string? errorMessage = null,
        string? completedModelId = null)
        => OnProgress?.Invoke(new ServeProgress
        {
            Stage = stage,
            FailedAtStage = failedAtStage,
            ErrorMessage = errorMessage,
            CompletedModelId = completedModelId,
        });
}
