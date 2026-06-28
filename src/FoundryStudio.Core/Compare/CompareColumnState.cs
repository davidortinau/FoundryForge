using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Compare;

/// <summary>
/// Mutable per-column state for the Compare workbench. One instance per selected model.
/// Updated by <see cref="FoundryStudio.App.Services.CompareOrchestrator"/> as tokens stream in;
/// consumed read-only by the Razor column component.
/// </summary>
public sealed class CompareColumnState
{
    public CompareColumnState(ModelInfo model)
    {
        Model = model;
    }

    public ModelInfo Model { get; }

    /// <summary>Accumulated text response so far (grows with each token chunk).</summary>
    public string Text { get; internal set; } = string.Empty;

    /// <summary>Live metrics snapshot — updated after every token and on completion.</summary>
    public GenerationMetrics? Metrics { get; internal set; }

    /// <summary>True while streaming is active for this column.</summary>
    public bool IsStreaming { get; internal set; }

    /// <summary>True once the stream has ended (naturally, cancelled, or with error).</summary>
    public bool IsDone { get; internal set; }

    /// <summary>True if the stream ended with an unrecoverable error.</summary>
    public bool IsError { get; internal set; }

    /// <summary>Error detail for display. Non-null only when <see cref="IsError"/> is true.</summary>
    public string? ErrorMessage { get; internal set; }

    /// <summary>How the generation ended. Unknown until the stream terminates.</summary>
    public StopReason StopReason { get; internal set; } = StopReason.Unknown;
}
