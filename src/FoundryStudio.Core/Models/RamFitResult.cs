namespace FoundryStudio.Core.Models;

public sealed record RamFitResult(
    RamFit Fit,
    double MarginGb,
    bool LongContextCaveat);

public enum RamFit
{
    Comfortable,
    Tight,
    Unlikely,
}
