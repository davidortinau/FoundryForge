namespace FoundryForge.Core.Models;

/// <summary>Informational variant descriptor (quant/device). No selection-to-load in M2 (that is M3).</summary>
public sealed record ModelVariant(
    string VariantId,
    string? Quantization,
    Device? Device,
    double? SizeGb);

public enum Device
{
    Cpu,
    Gpu,
    Npu,
}
