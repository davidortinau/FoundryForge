using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Catalog;

/// <summary>
/// Pinned-variant state (M3, US5). No fabrication: no variants -> HasVariants false and the UI shows an
/// honest "no variants reported". Pin only honors an id that actually exists. EffectiveVariantId is what the
/// UI passes as the variantId targeting parameter to Download/Load so the pin is honored.
/// </summary>
public sealed class VariantSelectionState
{
    private readonly IReadOnlyList<ModelVariant> _variants;

    public VariantSelectionState(IReadOnlyList<ModelVariant> variants)
    {
        _variants = variants ?? Array.Empty<ModelVariant>();
    }

    public string? PinnedVariantId { get; private set; }

    public bool HasVariants => _variants.Count > 0;

    public string? EffectiveVariantId =>
        PinnedVariantId ?? (_variants.Count > 0 ? _variants[0].VariantId : null);

    public void Pin(string variantId)
    {
        if (_variants.Any(v => string.Equals(v.VariantId, variantId, StringComparison.Ordinal)))
        {
            PinnedVariantId = variantId;
        }
        // unknown id ignored — never fabricate a selection
    }
}
