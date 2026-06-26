# Contract: Core pure-logic seams (new in M2)

All seams below live in `FoundryStudio.Core`, are **FL-free and dylib-free**, and are unit-tested in `tests/FoundryStudio.Tests` without a native Foundry Local dylib (FR-019, SC-003/SC-004).

---

## `CapabilityParser` — derive honest capabilities (R2)

`src/FoundryStudio.Core/Catalog/CapabilityParser.cs`

```csharp
public static ModelCapabilities Parse(
    string? capabilities,         // FL ModelInfo.Capabilities (free-form, may be null)
    string? inputModalities,      // FL ModelInfo.InputModalities (may be null)
    bool? supportsToolCalling);   // FL ModelInfo.SupportsToolCalling (nullable)
```

**Contract**
- `Vision == true` iff `inputModalities` contains an image/vision token (case-insensitive), else false.
- `ToolCalling == true` iff `supportsToolCalling == true`; `ToolCallingKnown == supportsToolCalling.HasValue`.
- `Reasoning == true` iff `capabilities` declares a reasoning token; **never** inferred from alias/name.
- Null/empty inputs ⇒ all flags false, `ToolCallingKnown=false` (renders "unknown", not "absent").
- Pure, deterministic, allocation-light; no fabrication (Constitution IV).

**Test fixtures**: (caps="reasoning", modalities="text,image", tool=true) → Vision+Tool+Reasoning; (null,null,null) → none + tool-unknown; (modalities="text", tool=false) → none, tool-known-false.

---

## `CuratedSelector` — deterministic curated default (R3, FR-008)

`src/FoundryStudio.Core/Catalog/CuratedSelector.cs`

```csharp
public static IReadOnlyList<ModelInfo> Select(IEnumerable<ModelInfo> all);
public static IReadOnlyList<string> CuratedAliases { get; }  // the app-defined allow-list
```

**Contract**
- Returns only models whose `Alias` is in `CuratedAliases`, in a deterministic order (allow-list order).
- A curated alias absent from `all` is silently skipped (graceful degradation, never throws).
- Stable/idempotent: same input ⇒ same output ordering.
- Makes **no** quality claim; the set is "FoundryStudio-curated", documented as such.

**Test fixtures**: full set incl. curated+non-curated → only curated, in allow-list order; missing curated alias → skipped; empty input → empty.

---

## `CatalogFacets` — honest facet option derivation (R4, FR-006)

`src/FoundryStudio.Core/Catalog/CatalogFacets.cs`

```csharp
public static CatalogFacets Derive(IEnumerable<ModelInfo> models);
// CatalogFacets { IReadOnlyList<string> Tasks; IReadOnlyList<string> Providers; IReadOnlyList<Device> Devices; }
```

**Contract**
- `Tasks`/`Providers`: distinct, non-empty, case-insensitively de-duplicated, sorted deterministically. Models with empty/unknown facet are **excluded** from the option list (not bucketed as a real value, US3 AC-6).
- `Devices`: distinct non-null `Device` values present in the catalog (CPU/GPU/NPU); null-device models contribute nothing.
- Pure/deterministic.

**Test fixtures**: mixed catalog with some empty Task/Provider and a null-Device model → options exclude the unknowns; duplicates collapsed.

---

## `CatalogFilterExtensions` — UNCHANGED behavior (FR-005/FR-007)

`src/FoundryStudio.Core/Catalog/CatalogFilterExtensions.cs` keeps its M1 `Matches`/`Apply` semantics. Only adjustment: null-safety for the now-nullable `ModelInfo.Device` (a `null` device matches **no** device filter; still matches when no device filter is set). Existing M1 tests remain valid; one added case covers a null-device model against a device filter.

---

## `RamFitHeuristic` — UNCHANGED (R5, optional)

No behavior change. If the optional RAM-fit hint is surfaced, callers pass `SizeGb` only when non-null; an unknown size means no fit hint is shown (honest), never a fabricated verdict.
