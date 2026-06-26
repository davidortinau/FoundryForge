namespace FoundryStudio.Core.Models;

public sealed record ModelVariant(
    string VariantId,
    string? Quantization,
    Device Device,
    double SizeGb);

public enum Device
{
    Cpu,
    Gpu,
    Npu,
}
