using FoundryForge.Core.Models;

namespace FoundryForge.Core.Catalog;

/// <summary>
/// Pure RAM-fit heuristic (FR-016, R5). Compares a model's on-disk size against <b>free</b> RAM with a
/// wide margin and a long-context KV-cache caveat. It deliberately NEVER returns a confident "this will
/// run great" verdict — it is a size-vs-free-RAM hint, not a guarantee (capability honesty).
/// </summary>
public static class RamFitHeuristic
{
    // Loading needs more than the raw weights: runtime, activations, and (growing) KV cache.
    private const double WeightOverheadFactor = 1.2;

    // Headroom beyond the estimated footprint before we call a fit "comfortable".
    private const double ComfortableHeadroomGb = 3.0;

    public static RamFitResult Evaluate(double modelSizeGb, double freeRamGb)
    {
        if (modelSizeGb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modelSizeGb));
        }

        if (freeRamGb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freeRamGb));
        }

        var estimatedFootprint = modelSizeGb * WeightOverheadFactor;
        var margin = Math.Round(freeRamGb - estimatedFootprint, 2);

        var fit = margin >= ComfortableHeadroomGb
            ? RamFit.Comfortable
            : margin >= 0
                ? RamFit.Tight
                : RamFit.Unlikely;

        // Even a "comfortable" verdict carries the caveat: long context windows grow the KV cache and
        // can push a borderline-OK model over the edge.
        var longContextCaveat = fit != RamFit.Unlikely;

        return new RamFitResult(fit, margin, longContextCaveat);
    }
}
