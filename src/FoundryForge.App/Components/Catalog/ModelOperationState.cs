namespace FoundryForge.App.Components.Catalog;

public enum ModelOpPhase
{
    Idle,
    Downloading,
    Cancelling,
    Loading,
    Unloading,
    Deleting,
    Failed,
    Busy,
}

public sealed class ModelOperationState
{
    public ModelOpPhase Phase { get; set; } = ModelOpPhase.Idle;

    public double? ProgressPercent { get; set; }

    public bool AutoLoadAfterDownload { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public CancellationTokenSource? Cts { get; set; }

    public bool IsWorking =>
        Phase is ModelOpPhase.Downloading
            or ModelOpPhase.Cancelling
            or ModelOpPhase.Loading
            or ModelOpPhase.Unloading
            or ModelOpPhase.Deleting;

    public void Reset()
    {
        Phase = ModelOpPhase.Idle;
        ProgressPercent = null;
        ErrorMessage = null;
        Cts?.Dispose();
        Cts = null;
    }

    public void Fail(string message, bool busy = false)
    {
        Phase = busy ? ModelOpPhase.Busy : ModelOpPhase.Failed;
        ErrorMessage = message;
        ProgressPercent = null;
        Cts?.Dispose();
        Cts = null;
    }
}
