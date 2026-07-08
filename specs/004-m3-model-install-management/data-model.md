# Phase 1 Data Model: M3 — Model Install & Management

M3 adds **no persistent schema** (the model cache and `AppSettings` are unchanged). The entities below are (a) **pure-logic Core seams** (dylib-free, unit-tested — `FoundryForge.Core`) and (b) **transient UI view-model state** (`FoundryForge.App`). Existing entities (`ModelInfo`, `ModelVariant`, `AppSettings`) are consumed unchanged for the P1/P2 stories.

---

## Existing entities (consumed, unchanged)

### `ModelInfo` — `src/FoundryForge.Core/Models/ModelInfo.cs`
Consumed fields in M3: `Alias`, `Id`, `SizeGb` (disk-fit, nullable = unknown), `Variants`, `IsCached`, `IsLoaded`, `Device`. **No schema change** for P1/P2.

### `ModelVariant` — `src/FoundryForge.Core/Models/ModelVariant.cs`
`VariantId` (= FL variant `Info.Id`), `Quantization`, `Device`, `SizeGb`. The unit of selection in US5; `VariantId` is the value passed as the additive `variantId` targeting parameter (research R7).

### `AppSettings` — `src/FoundryForge.Core/Models/AppSettings.cs`
`record (string ModelCacheDirectory, string? DefaultModel, AppTheme Theme, int SchemaVersion)`. US7 edits `ModelCacheDirectory` via `ISettingsService` (consent-gated, no-wipe). Edge case: deleting a model equal to `DefaultModel` must leave `DefaultModel` honest (cleared/flagged), not dangling.

---

## New Core seam entities (pure, dylib-free)

### `DiskFitResult` + `DiskFit` — `src/FoundryForge.Core/Models/DiskFitResult.cs`
Mirrors `RamFitResult`/`RamFit`.
```csharp
public sealed record DiskFitResult(DiskFit Fit, double? MarginGb);
public enum DiskFit { Fits, Warn, Unknown }
```
| Field | Meaning |
|-------|---------|
| `Fit` | `Fits` (no warning) / `Warn` (non-blocking warning) / `Unknown` (null size — honest, FR-024) |
| `MarginGb` | `freeDiskGb - estimatedFootprint`, rounded; `null` when `Fit == Unknown` |

**Producer**: `DiskFitHeuristic.Evaluate(double? modelSizeGb, double freeDiskGb)` (research R3). Pure; no I/O. State transitions: none (stateless verdict).

### `VariantSelectionState` — `src/FoundryForge.Core/Catalog/VariantSelectionState.cs`
Pure per-model pinned-variant state (US5).
| Concept | Rule |
|---------|------|
| `Variants` | the model's reported `ModelVariant` list |
| `PinnedVariantId` | `string?` — currently pinned variant id; `null` ⇒ default |
| Default selection | first/declared default when variants exist; honest **"no variants reported"** when empty (FR-021) — never fabricated |
| `Pin(variantId)` | sets `PinnedVariantId` iff it exists in `Variants`; ignores unknown ids |
| `Effective` | `PinnedVariantId ?? <default>`; `null` when no variants |
**State transitions**: `unset → pinned(id) → re-pinned(id') `; no-variants state is terminal/honest. Unit-tested: default, pin, re-pin, no-variants (SC-008).

### `CatalogGrouping` (cached/available partition) — `src/FoundryForge.Core/Catalog/CatalogGrouping.cs`
Pure partition for US4 / KI-009 (research R5).
```csharp
public static (IReadOnlyList<ModelInfo> Cached, IReadOnlyList<ModelInfo> Available)
    Partition(IEnumerable<ModelInfo> all, ISet<string> cachedAliases);
```
| Rule | Detail |
|------|--------|
| Membership | a model is **Cached** iff its `Alias` ∈ `cachedAliases` (the authoritative cached set from `ListCachedAsync`), else **Available** |
| Exactly one group | every model appears in exactly one list (SC-007) |
| KI-009 | keys off the trusted cached-list membership, **not** a re-applied `CachedOnly`/`info.Cached` filter |
| Stable order | preserves input order within each group |

---

## New UI view-model entities (transient, `FoundryForge.App`)

### `ModelOperationState` — `src/FoundryForge.App/Components/Catalog/ModelOperationState.cs`
Per-model transient state driving card rendering (not persisted).
```csharp
public enum ModelOpPhase { Idle, Downloading, Cancelling, Loading, Unloading, Deleting, Failed, Busy }
```
| Field | Meaning |
|-------|---------|
| `Phase` | current operation phase (above) |
| `ProgressPercent` | `double?` — real percent from `IProgress<double>`; `null` ⇒ indeterminate (no progress source — honest, FR-002) |
| `AutoLoadAfterDownload` | `bool` — US1 auto-load choice |
| `ErrorMessage` | `string?` — honest diagnosed cause on `Failed`/`Busy` (FR-005/FR-009) |
| `Cts` | `CancellationTokenSource?` — backs Cancel (US1) |

**State transitions** (per model):
```
Idle ──download──▶ Downloading ──progress*──▶ Downloading
Downloading ──cancel──▶ Cancelling ──▶ Idle (authoritative cached recheck; no partial shown as ready, R1)
Downloading ──complete (auto-load off)──▶ Idle (now cached)
Downloading ──complete (auto-load on)──▶ Loading ──▶ Idle (now loaded)
Downloading ──error──▶ Failed(message) ──▶ Idle (not cached, FR-005)
Idle(cached) ──load──▶ Loading ──▶ Idle(loaded) | Busy(ModelBusyException, FR-009)
Idle(loaded) ──unload──▶ Unloading ──▶ Idle(cached)
Idle(cached) ──delete(confirmed)──▶ Deleting ──▶ Idle(not cached) | Busy
```
After any terminal transition, the card's `IsCached`/`IsLoaded` are recomputed from **authoritative** `ListCachedAsync`/`ListLoadedAsync` (refresh-on-mutation, research R6) — never optimistic.

### `DeleteConfirmation` (consent object) — modeled in `ConfirmDialog.razor` state
Per-action consent for the load-bearing Delete flow (US3) and the cache-dir change (US7).
| Field | Meaning |
|-------|---------|
| `ModelAlias` / `Title` | the exact model named in the prompt (FR-011) |
| `ConsequenceText` | "Confirming frees disk space / nothing will be moved or deleted" (FR-011/FR-026) |
| `OnConfirm` | maps to `DeleteFromCacheAsync(alias, userConfirmed: true, ct)` (FR-013) or the cache-dir `UpdateAsync` |
| `OnCancel` | no-op; closes dialog, state unchanged (FR-012) |
Invariant: a single Delete activation only **opens** this dialog — it never deletes (FR-010). No destructive default (FR-014).

### `CatalogGroupedViewState` (extension of `CatalogViewState`)
`CatalogViewState.cs` gains: the grouped (Cached / Available) projection from `CatalogGrouping.Partition`, the authoritative **loaded set** (for the app-wide indicator + per-card badge), and a per-alias `ModelOperationState` map. Recompute-on-mutation refreshes all three. The existing M2 filter/facet/curated logic is unchanged (surgical).

### `SettingsViewState` — `Settings.razor`
| Field | Meaning |
|-------|---------|
| `ModelCacheDirectory` | current value from `GetAsync` (FR-025) |
| `PendingDirectory` | edited-but-unsaved value |
| `ValidationError` | honest error for invalid/unwritable dir; previous value retained (FR-027) |
| `FreeDiskGb` | derived via `DriveInfo` for the disk-fit context (research R3) |

---

## Entity → requirement → seam map

| Entity | Stories | FRs | Seam / file | Verified by |
|--------|---------|-----|-------------|-------------|
| `DiskFitResult`/`DiskFitHeuristic` | US6 | FR-022..024 | Core (pure) | `DiskFitHeuristicTests` (SC-009) |
| `VariantSelectionState` | US5 | FR-019..021 | Core (pure) | `VariantSelectionStateTests` (SC-008) |
| `CatalogGrouping` | US4 | FR-016..018 | Core (pure) | `CatalogGroupingTests` (SC-007, KI-009) |
| Delete consent gate | US3 | FR-010..015 | `DeleteFromCacheAsync` (existing) + `ConfirmDialog` | `DeleteConsentGateTests` (SC-006) + DOM |
| `ModelOperationState` | US1/US2 | FR-001..009 | App (transient) | DevFlow DOM e2e (SC-001..004) |
| Cache-dir change | US7 | FR-025..027 | `ISettingsService` + `SettingsViewState` | settings-seam unit + DOM (SC-010) |
| Variant targeting delta | US5 | FR-019/020 | `IFoundryCatalogService` (additive `variantId`) | unit + DOM |
