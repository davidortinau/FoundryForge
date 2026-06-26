# Phase 1 Data Model: M2 — Catalog Browse + Discovery

**Feature**: `003-m2-catalog-browse-discovery` · **Date**: 2026-06-25
**Source of truth for field availability**: `research.md` R1/R2 (reflected FL `1.2.3` surface). All DTOs stay **FL-free** in `FoundryStudio.Core` (Constitution V / DEC-004 — the UI never touches the FL SDK).

---

## 1. Enriched `ModelInfo` (Core DTO — extended)

`src/FoundryStudio.Core/Models/ModelInfo.cs`. M1 record extended with M2 honesty-bearing fields. **`SizeGb` becomes nullable** so "unknown" is representable (FR-012 — never `0 GB` as a real size).

```csharp
public sealed record ModelInfo(
    string Alias,
    string Id,
    string DisplayName,
    double? SizeGb,                         // CHANGED: was double=0 stub → null = unknown (FL FileSizeMb/1024)
    Device? Device,                         // CHANGED: was Device(=Gpu stub) → null = unknown (FL Runtime.DeviceType; Invalid→null)
    string Task,                            // FL ModelInfo.Task ("" = unknown)
    string Provider,                        // FL ModelInfo.ProviderType ("" = unknown)
    IReadOnlyList<ModelVariant> Variants,   // FL IModel.Variants → mapped
    bool IsCached,
    bool IsLoaded,
    // ---- M2 additions (all honest-nullable / empty = unknown) ----
    string? ExecutionProvider = null,       // FL Runtime.ExecutionProvider
    int? ContextLength = null,              // FL ModelInfo.ContextLength
    int? MaxOutputTokens = null,            // FL ModelInfo.MaxOutputTokens
    string? License = null,                 // FL ModelInfo.License
    string? LicenseDescription = null,      // FL ModelInfo.LicenseDescription (tooltip)
    string? Publisher = null,               // FL ModelInfo.Publisher
    string? ModelType = null,               // FL ModelInfo.ModelType (e.g. "ONNX")
    ModelCapabilities Capabilities = default); // derived, R2
```

**Validation / honesty rules**
- `SizeGb`: `null` when `FileSizeMb` is null/0 → card renders "size unknown". Non-null only from real `FileSizeMb`.
- `Device`: `null` when `Runtime.DeviceType == Invalid` or runtime absent. Filtering: a null-device model matches no device facet (R4).
- `Task`/`Provider`: empty string = unknown (consistent with M1 `CatalogFilterExtensions` which treats null/whitespace filter as match-all; an empty model facet simply won't match a chosen facet).
- Backward compat: existing callers/tests that read `SizeGb`/`Device` must handle nullability; `RamFitHeuristic` consumes `SizeGb ?? <skip>` (only evaluated when size known).

**Breaking-change note**: `SizeGb double → double?` and `Device → Device?` touch `CatalogFilterExtensions`, `RamFitHeuristic` callers, and existing tests. These edits are in-scope (the M1 stub explicitly deferred enrichment to M2 via `TODO(M2)`), surgical, and covered by updated unit tests.

---

## 2. `ModelCapabilities` (new Core value type)

`src/FoundryStudio.Core/Models/ModelCapabilities.cs`. A small honest capability set (R2). Each flag is **tri-state-by-omission**: present = FL declared it; absent = not declared (rendered as "not reported", never "absent").

```csharp
public readonly record struct ModelCapabilities(
    bool Vision,            // InputModalities contains image/vision token
    bool ToolCalling,       // ModelInfo.SupportsToolCalling == true
    bool Reasoning,         // ModelInfo.Capabilities string declares reasoning
    bool ToolCallingKnown); // false when SupportsToolCalling == null → render "unknown"
```

Rationale: `SupportsToolCalling` is `bool?` in FL; `ToolCallingKnown=false` lets the card distinguish "tools: no" from "tools: unknown" (Constitution IV).

---

## 3. `ModelVariant` (Core DTO — extended)

`src/FoundryStudio.Core/Models/ModelVariant.cs`. Informational only (no selection-to-load in M2 — that is M3).

```csharp
public sealed record ModelVariant(
    string VariantId,
    string? Quantization,   // parsed from variant alias/id when present; null = unknown
    Device? Device,         // CHANGED nullable: FL variant Runtime.DeviceType (Invalid→null)
    double? SizeGb);        // CHANGED nullable: FL variant FileSizeMb/1024 (null = unknown)

public enum Device { Cpu, Gpu, Npu }   // unchanged (FL Invalid maps to a null Device?, not a new enum member)
```

Source: `IModel.Variants[i].Info` → `Runtime.DeviceType`, `FileSizeMb`, `Id` (R1).

---

## 4. `CatalogFilter` (Core DTO — UNCHANGED)

`src/FoundryStudio.Core/Models/CatalogFilter.cs` stays exactly as M1 (`Device?`/`Task`/`Provider`/`SearchText`/`CachedOnly`). FR-005/FR-007 forbid a parallel predicate. `CatalogFilterExtensions.Matches` behavior is preserved; the only adjustment is null-safety for the now-nullable `model.Device` (a null device never matches a device facet).

---

## 5. `CatalogViewState` (App view-model — new, UI layer)

Lives in `src/FoundryStudio.App/Components/Catalog/` (UI, not Core — it is Blazor render state). The mutually-exclusive screen state (FR-014/015/016, US6) plus the active query/filter and curated/full toggle.

```
CatalogViewState
├── Status : enum { Loading, Populated, Empty, Error }   // mutually exclusive, honest states
├── ErrorMessage : string?                               // diagnosed cause for Error (Constitution: not generic)
├── AllModels : IReadOnlyList<ModelInfo>                 // full enriched catalog (source of truth)
├── Filter : CatalogFilter                               // bound to search box + facet controls + cached toggle
├── ViewMode : enum { Curated, Full }                    // FR-008 curated default; toggle to Full
├── Visible : IReadOnlyList<ModelInfo>                   // derived: curated/full → CatalogFilter.Apply
└── Facets : CatalogFacets                               // distinct Task/Provider option lists (derived)
```

**Derivation pipeline (pure, deterministic)**:
`Visible = (ViewMode == Curated && Filter.IsEmpty) ? CuratedSelector.Select(AllModels) : Filter.Apply(AllModels)`
- Any non-empty search/filter forces evaluation over `AllModels` (full catalog) regardless of `ViewMode` (FR-009).
- `Status` resolves to `Empty` when `Visible` is empty but `AllModels` is non-empty and a filter is active; `Populated` otherwise; `Loading`/`Error` set by the fetch.

---

## 6. Pure-logic Core seams (dylib-free testable — FR-019)

| Seam | File | Responsibility |
|------|------|----------------|
| `CuratedSelector` | `src/FoundryStudio.Core/Catalog/CuratedSelector.cs` | Deterministic curated allow-list selection (R3); `Select(models)` returns curated subset in stable order |
| `CatalogFacets` | `src/FoundryStudio.Core/Catalog/CatalogFacets.cs` | `Derive(models)` → distinct non-empty Task/Provider/Device option lists (R4); excludes unknown |
| `CatalogFilterExtensions` | existing | unchanged behavior; null-device safety only |
| `ModelCapabilities` derivation | `src/FoundryStudio.Core/Catalog/CapabilityParser.cs` | `Parse(capabilitiesString, inputModalities, supportsToolCalling)` → `ModelCapabilities` (R2) |

The **FL→Core mapping helper** (`ModelInfoMapper`) is the one piece that touches FL types, so it lives in `FoundryStudio.Foundry` (not Core) — but it is split so the *post-FL* pure transformation (capability parsing, size conversion, unknown handling) is in Core and unit-tested against plain fixtures (FR-019: "metadata-mapping logic MUST be testable from FL-metadata fixtures without a dylib"). The Foundry-side mapper only reads FL properties into the Core-side pure functions.

---

## 7. Entity relationships

```
FL ICatalog ──ListModelsAsync()──▶ IModel[] (FL types, Foundry project only)
                                      │  .Info (ModelInfo), .Variants
                                      ▼
              FoundryCatalogService.MapEnriched(IModel)         ← src/FoundryStudio.Foundry
                  ├─ size  = FileSizeMb/1024 (null-safe)
                  ├─ device= Runtime.DeviceType (Invalid→null)
                  ├─ caps  = CapabilityParser.Parse(...)        ← Core pure
                  └─ variants = IModel.Variants → ModelVariant[]
                                      ▼
                         Core ModelInfo (FL-free DTO)           ← crosses the seam
                                      ▼
   IFoundryCatalogService.BrowseAsync ──▶ App CatalogViewState  ← src/FoundryStudio.App
                  ├─ CuratedSelector.Select / CatalogFilter.Apply (Core pure)
                  └─ render: CatalogList → ModelCard × N (+ badges, states)
```

No new persistence. No new service registration beyond what already exists (`IFoundryCatalogService` is already DI-registered, M1). Catalog data is transient/in-memory per the M2 read-only browse session; the FL SDK's own catalog caching (PLAN.md ~6h) is upstream and untouched.
