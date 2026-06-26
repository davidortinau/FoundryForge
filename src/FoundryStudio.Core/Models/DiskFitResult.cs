namespace FoundryStudio.Core.Models;

/// <summary>Non-blocking disk-fit verdict for a download (M3). Honest: Unknown when size is unknown.</summary>
public sealed record DiskFitResult(DiskFit Fit, double? MarginGb);

public enum DiskFit
{
    Fits,
    Warn,
    Unknown,
}
