# Contract: Core pure-logic seams (new/extended in M3)

All seams below live in `FoundryForge.Core`, are **FL-free and dylib-free**, and are unit-tested in `tests/FoundryForge.Tests` without a native Foundry Local dylib (FR-034, SC-006/007/008/009). They mirror the M2 precedent (`RamFitHeuristic`, `CuratedSelector`, `CatalogFacets`).

---

## `DiskFitHeuristic` — non-blocking disk-fit verdict (US6, R3)

`src/FoundryForge.Core/Catalog/DiskFitHeuristic.cs`

```csharp
public static DiskFitResult Evaluate(double? modelSizeGb, double freeDiskGb);
```

**Contract**
- `modelSizeGb is null` ⇒ `DiskFit.Unknown`, `MarginGb = null` — honest "size unknown — can't check fit" (FR-024). **Never** a fabricated fits/won't-fit verdict.
- `freeDiskGb < 0` ⇒ `ArgumentOutOfRangeException` (mirrors `RamFitHeuristic`).
- `estimatedFootprint = modelSizeGb * SafetyFactor` (documented margin in XML-doc, wide/honest like RamFit).
- `estimatedFootprint <= freeDiskGb` ⇒ `DiskFit.Fits`; else `DiskFit.Warn`.
- `MarginGb = round(freeDiskGb - estimatedFootprint, 2)` for known sizes.
- Pure, deterministic, allocation-light; no I/O (free-disk is read by the caller via `DriveInfo`, R3). Never blocks (warn-not-block, FR-023 — enforced by the UI, the seam only classifies).

**Test fixtures** (`DiskFitHeuristicTests`, SC-009): (size=2, free=100)→Fits; (size=50, free=10)→Warn; (size=null, free=100)→Unknown; (free=-1)→throws; boundary (footprint==free)→Fits.

---

## `CatalogGrouping` — cached vs available partition (US4, KI-009, R5)

`src/FoundryForge.Core/Catalog/CatalogGrouping.cs`

```csharp
public static (IReadOnlyList<ModelInfo> Cached, IReadOnlyList<ModelInfo> Available)
    Partition(IEnumerable<ModelInfo> all, ISet<string> cachedAliases);
```

**Contract**
- A model is in **Cached** iff `cachedAliases.Contains(model.Alias)` (the authoritative cached set from `ListCachedAsync`); otherwise **Available**.
- Every input model appears in **exactly one** output list (SC-007); no duplicates, no drops.
- KI-009: membership is keyed off the **trusted cached list**, NOT a re-applied `CachedOnly`/`info.Cached` filter — the cached group is never empty when cached models exist, never padded with not-cached models (FR-017).
- Preserves input order within each group; `null`/empty inputs ⇒ empty groups (never throws).
- Pure, deterministic, dylib-free.

**Test fixtures** (`CatalogGroupingTests`, SC-007): mixed list with 2 cached aliases → correct split; a model whose `IsCached` flag disagrees with the cached set is grouped by the **set** (KI-009 proof); empty cachedAliases → all Available; duplicate-free.

---

## `VariantSelectionState` — pinned-variant state (US5, R7)

`src/FoundryForge.Core/Catalog/VariantSelectionState.cs`

```csharp
public sealed class VariantSelectionState
{
    public VariantSelectionState(IReadOnlyList<ModelVariant> variants);
    public string? PinnedVariantId { get; }
    public string? EffectiveVariantId { get; }   // PinnedVariantId ?? default; null when no variants
    public bool HasVariants { get; }              // false ⇒ honest "no variants reported" (FR-021)
    public void Pin(string variantId);            // no-op if id not in variants
}
```

**Contract**
- No variants ⇒ `HasVariants == false`, `EffectiveVariantId == null`; UI shows honest "no variants reported" — **never** a fabricated option (FR-021).
- `Pin(id)` sets `PinnedVariantId` only if `id` exists in `variants`; unknown ids are ignored (no fabrication).
- `EffectiveVariantId` is the value the UI passes as the `variantId` targeting parameter to `DownloadAsync`/`LoadAsync` (R7) — i.e. the pin is **honored** (FR-020).
- Pure; no FL, no I/O.

**Test fixtures** (`VariantSelectionStateTests`, SC-008): multi-variant default selection; `Pin` honored; `Pin(unknown)` ignored; empty variants → `HasVariants=false`, effective null.

---

## Consent-gate coverage (US3, R8) — existing method, new dylib-free test

`DeleteFromCacheAsync(string alias, bool userConfirmed, CancellationToken)` already throws `InvalidOperationException` when `!userConfirmed` **before** any FL resolution (`FoundryCatalogService.cs` L120-126). M3 adds dylib-free coverage proving the gate.

**Contract (asserted by `DeleteConsentGateTests`, SC-006)**
- `userConfirmed: false` ⇒ throws (or no-ops) and **removes nothing**, reached before any FL/dylib call.
- `userConfirmed: true` ⇒ proceeds through the `IModelStateGate` (Drain) so it can't tear an in-flight op (FR-015).
- Test strategy: assert the guard via the public method using a seam that needs no native dylib (e.g. the guard clause throws before `ResolveModelAsync`), or a fake catalog substitute — proving consent without touching any real cache (FR-036).

---

## Notes
- These seams contain **no fabrication**: every "unknown"/"no variants"/"warn" state is honest and derived from inputs, satisfying Constitution IV capability honesty.
- All four are independently unit-testable with zero native dependencies, satisfying FR-034 and keeping the CI seam gate green.
