using FoundryForge.Core.Abstractions;
using FoundryForge.Core.Chat;
using FoundryForge.Core.Models;
using Microsoft.Extensions.AI;

namespace FoundryForge.Core.Compare;

/// <summary>
/// Orchestrates concurrent N-model compare runs for the Compare workbench.
/// Each model gets its own <see cref="CompareMetrics"/> accumulator and <see cref="CompareColumnState"/>.
/// Per-column try/catch ensures one column faulting does not affect the others.
/// Injectable via <see cref="IChatService"/> — testable with any stub that emits known tokens
/// without a real Foundry Local instance (no native dylib required).
/// </summary>
public sealed class CompareOrchestrator : IAsyncDisposable
{
    private readonly IChatService _chatService;
    private CancellationTokenSource? _cts;
    private List<CompareColumnState> _columns = new();

    /// <summary>
    /// Fired after each column state change. Subscribe and call
    /// <c>await InvokeAsync(StateHasChanged)</c> in the Razor component.
    /// </summary>
    public event Action? OnChanged;

    /// <summary>Current per-column state, one entry per selected model.</summary>
    public IReadOnlyList<CompareColumnState> Columns => _columns;

    /// <summary>True while at least one column is still streaming.</summary>
    public bool IsRunning { get; private set; }

    public CompareOrchestrator(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Start concurrent generation for each model in <paramref name="models"/> using the shared
    /// <paramref name="prompt"/>. Cancels any prior run first.
    /// Returns when all columns have finished (naturally, cancelled, or with error).
    /// </summary>
    public async Task RunAsync(
        IReadOnlyList<ModelInfo> models,
        string prompt,
        InferenceParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await CancelAndResetAsync();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _columns = models.Select(m => new CompareColumnState(m)).ToList();
        IsRunning = true;
        NotifyChanged();

        var linkedToken = _cts.Token;
        var tasks = _columns.Select(col => RunColumnAsync(col, prompt, parameters, linkedToken)).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            IsRunning = false;
            NotifyChanged();
        }
    }

    /// <summary>Cancel all in-flight column streams.</summary>
    public void Cancel() => _cts?.Cancel();

    private async Task RunColumnAsync(
        CompareColumnState column,
        string prompt,
        InferenceParameters? parameters,
        CancellationToken ct)
    {
        var messages = new[] { new ChatMessage(ChatRole.User, prompt) };
        var p = parameters ?? InferenceParameters.Defaults;
        var options = p.ToChatOptions(column.Model.Alias);

        var accumulator = new CompareMetrics();
        accumulator.OnSend(DateTimeOffset.UtcNow);

        column.IsStreaming = true;
        var stopReason = StopReason.Unknown;

        try
        {
            await foreach (var update in _chatService.StreamAsync(messages, options, ct).WithCancellation(ct))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    accumulator.OnToken(DateTimeOffset.UtcNow);
                    column.Text += update.Text;
                }

                // Extract real usage from the terminal frame if the stream carries it.
                foreach (var content in update.Contents)
                {
                    if (content is UsageContent usage && usage.Details is { } details)
                    {
                        int? total = details.TotalTokenCount is { } t ? (int)t : null;
                        int? output = details.OutputTokenCount is { } o ? (int)o : null;
                        accumulator.OnUsage(total, output);
                    }
                }

                if (update.FinishReason is { } reason)
                    stopReason = MapStopReason(reason);

                column.Metrics = accumulator.Snapshot(stopReason);
                NotifyChanged();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopReason = StopReason.UserCancelled;
        }
        catch (Exception ex)
        {
            column.IsError = true;
            column.ErrorMessage = ex.Message;
            stopReason = StopReason.Error;
        }
        finally
        {
            column.IsStreaming = false;
            column.IsDone = true;
            column.StopReason = stopReason;
            column.Metrics = accumulator.Snapshot(stopReason);
            NotifyChanged();
        }
    }

    private async Task CancelAndResetAsync()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _columns = new();
        IsRunning = false;
        NotifyChanged();
        // Yield to let any in-flight NotifyChanged callbacks drain before re-initialising.
        await Task.Yield();
    }

    private static StopReason MapStopReason(ChatFinishReason reason)
    {
        if (reason == ChatFinishReason.Stop) return StopReason.Natural;
        if (reason == ChatFinishReason.Length) return StopReason.MaxTokens;
        if (reason == ChatFinishReason.ToolCalls) return StopReason.ToolCalls;
        return StopReason.Unknown;
    }

    private void NotifyChanged() => OnChanged?.Invoke();

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return ValueTask.CompletedTask;
    }
}
