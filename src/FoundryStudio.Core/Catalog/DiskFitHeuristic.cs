using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Catalog;

/// <summary>
/// Pure, non-blocking disk-fit heuristic (M3, US6). Compares a model's size against free disk with a wide
/// safety margin. NEVER fabricates a verdict: unknown size yields <see cref="DiskFit.Unknown"/>. Warn-not-block
/// (the UI never prevents a download; this only classifies). No I/O — free disk is read by the caller.
/// </summary>
public static class DiskFitHeuristic
{
    // Download needs more than the on-disk model: temp/extraction headroom. Documented, honest margin.
    private const double SafetyFactor = 1.25;

    public static DiskFitResult Evaluate(double? modelSizeGb, double freeDiskGb)
    {
        if (freeDiskGb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freeDiskGb));
        }

        if (modelSizeGb is not double size || size <= 0)
        {
            return new DiskFitResult(DiskFit.Unknown, null); // honest: can't check fit without a real size
        }

        var estimatedFootprint = size * SafetyFactor;
        var margin = Math.Round(freeDiskGb - estimatedFootprint, 2);
        var fit = estimatedFootprint <= freeDiskGb ? DiskFit.Fits : DiskFit.Warn;
        return new DiskFitResult(fit, margin);
    }
}
