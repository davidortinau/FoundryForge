# Phase 0 Research: M2 — Catalog Browse + Discovery

**Feature**: `003-m2-catalog-browse-discovery` · **Date**: 2026-06-25
**Inputs**: `spec.md`, `.specify/memory/constitution.md`, `docs/PLAN.md` (M2 line + parity map + positioning), `.squad/decisions.md` (DEC-004, DEC-014, DEC-016), `KNOWN-ISSUES.md` (KI-001/006/007/008), M1 code under `src/`, and the real Foundry Local `1.2.3` managed assembly.

This document resolves every `NEEDS CLARIFICATION` for M2. The two load-bearing outputs are **(R1) the FL metadata-availability decision** (which rich fields are real vs must render "unknown / not provided") and **(R6) the screenshot-evidence decision**.

---

## R1 — Foundry Local `1.2.3` metadata surface (THE key decision)

**Decision**: Map the enriched `ModelInfo`/`ModelVariant` cards from the **real** `Microsoft.AI.Foundry.Local.ModelInfo` / `Runtime` / `IModel` surface confirmed below; render an explicit "unknown / not provided" wherever a value is genuinely `null`, empty, or `Invalid` (Constitution IV).

**Method / citation (Constitution I)**: Reflected the actual public surface of the pinned assembly
`~/.nuget/packages/microsoft.ai.foundry.local/1.2.3/lib/net8.0/Microsoft.AI.Foundry.Local.dll`
(DEC-005 pin) on real hardware. Reflection output (declared instance members):

```
interface Microsoft.AI.Foundry.Local.IModel
  prop string                 Alias
  prop string                 Id
  prop ModelInfo              Info
  prop IReadOnlyList<IModel>  Variants
  // methods (M1-confirmed): IsCachedAsync, IsLoadedAsync, GetPathAsync,
  //   DownloadAsync, LoadAsync, UnloadAsync, RemoveFromCacheAsync, SelectVariant,
  //   GetChatClientAsync/GetEmbeddingClientAsync/GetAudioClientAsync

type Microsoft.AI.Foundry.Local.ModelInfo
  prop string         Alias
  prop bool           Cached
  prop string         Capabilities
  prop int?           ContextLength
  prop long           CreatedAtUnix
  prop string         DisplayName
  prop long?          FileSizeMb
  prop string         Id
  prop string         InputModalities
  prop string         License
  prop string         LicenseDescription
  prop int?           MaxOutputTokens
  prop string         MinFLVersion
  prop ModelSettings  ModelSettings        // Parameter[]
  prop string         ModelType
  prop string         Name
  prop string         OutputModalities
  prop PromptTemplate PromptTemplate        // System/User/Assistant/Prompt
  prop string         ProviderType
  prop string         Publisher
  prop Runtime        Runtime               // DeviceType + ExecutionProvider
  prop bool?          SupportsToolCalling
  prop string         Task
  prop string         Uri
  prop int            Version

type Microsoft.AI.Foundry.Local.Runtime
  prop DeviceType DeviceType                // enum: Invalid, CPU, GPU, NPU
  prop string     ExecutionProvider

type Microsoft.AI.Foundry.Local.ModelVariant : IModel-shaped
  prop string Alias; prop string Id; prop ModelInfo Info; prop IReadOnlyList<IModel> Variants; prop int Version

enum Microsoft.AI.Foundry.Local.DeviceType { Invalid, CPU, GPU, NPU }
```

**Field-by-field mapping decision** (Core `ModelInfo` ← FL `ModelInfo`/`Runtime`/`IModel`):

| Card field (Core DTO)      | FL source                              | Honest fallback when absent |
|----------------------------|----------------------------------------|-----------------------------|
| `Alias`                    | `IModel.Alias` / `ModelInfo.Alias`     | n/a (always present)        |
| `Id`                       | `IModel.Id`                            | n/a                         |
| `DisplayName`              | `ModelInfo.DisplayName` → `Name` → `Alias` | falls back up the chain |
| `SizeGb` (now `SizeGb?`)   | `ModelInfo.FileSizeMb / 1024.0`        | `null` ⇒ "unknown / not provided" (**never `0 GB`**) |
| `Device`                   | `Runtime.DeviceType` (CPU/GPU/NPU)     | `Invalid`/null ⇒ "unknown" (NOT defaulted to GPU) |
| `ExecutionProvider` (new)  | `Runtime.ExecutionProvider`            | empty ⇒ "unknown"           |
| `Task`                     | `ModelInfo.Task`                       | empty ⇒ "unknown"           |
| `Provider`                 | `ModelInfo.ProviderType`               | empty ⇒ "unknown"; `Publisher` shown as a secondary line |
| `Publisher` (new)          | `ModelInfo.Publisher`                  | empty ⇒ omit                |
| `ContextLength` (new)      | `ModelInfo.ContextLength`              | `null` ⇒ "unknown"          |
| `MaxOutputTokens` (new)    | `ModelInfo.MaxOutputTokens`            | `null` ⇒ omit/"unknown"     |
| `License` (new)            | `ModelInfo.License` (+ `LicenseDescription` tooltip) | empty ⇒ "unknown" |
| `Capabilities` (new)       | derived — see R2                       | empty set ⇒ show none, never fabricate |
| `Variants`                 | `IModel.Variants` → each `.Info` (FileSizeMb, Runtime.DeviceType, Id) | empty ⇒ "no variants reported" |
| `IsCached`                 | `ModelInfo.Cached` (or `IsCachedAsync`)| n/a                         |
| `IsLoaded`                 | `IsLoadedAsync` / loaded-list cross-ref | n/a                        |
| `ModelType` (new, info)    | `ModelInfo.ModelType` (e.g. ONNX)      | empty ⇒ omit                |

**Rationale**: Every field on the M2 card now traces to a concrete, reflected FL property — no invented confidence (Constitution I). The M1 `MapBasic` stub (`SizeGb: 0`, hardcoded `Device.Gpu`, empty `Task`/`Provider`/`Variants`) is replaced by `ModelInfo.FileSizeMb`, `Runtime.DeviceType`, `ModelInfo.Task`, `ModelInfo.ProviderType`, and `IModel.Variants`. `FileSizeMb`, `ContextLength`, `MaxOutputTokens`, and `SupportsToolCalling` are genuinely **nullable** in FL, so the DTO must carry nullability and the card must show "unknown / not provided" (FR-012, SC-007) rather than a fabricated default.

**Alternatives considered**:
- *Keep `MapBasic` defaults and hard-code plausible values* — rejected: fabricated metadata violates Constitution IV; spec FR-011/FR-012 explicitly forbid it.
- *Scrape model metadata from a side JSON / HuggingFace* — rejected: out of seam, not FL-sourced, unverifiable, and not needed (the SDK exposes the fields).
- *Call `GetModelVariantAsync(modelId)` per card for variants* — rejected for the list path: `IModel.Variants` already carries the variant collection in one `ListModelsAsync` round-trip; `GetModelVariantAsync`/`GetVariantsAsync` stays for the single-model detail path.

---

## R2 — Capability honesty: vision / tool / reasoning (Constitution IV)

**Decision**: Derive a small, honest capability set and show **only** what FL actually reports:
- **Tool**: `ModelInfo.SupportsToolCalling == true` ⇒ show a "tool" capability; `false` ⇒ do not show; `null` ⇒ "unknown" (omit, do not assert absence).
- **Vision**: `ModelInfo.InputModalities` contains an image/vision token (e.g. `image`) ⇒ show "vision"; otherwise do not show. (`OutputModalities` reserved for future multimodal output.)
- **Reasoning**: only if `ModelInfo.Capabilities` (the FL free-form capability string) contains a reasoning token. There is **no dedicated reasoning flag** in FL `1.2.3`, so "reasoning" is shown strictly when FL's own `Capabilities` string declares it — never inferred from the model name.

**Rationale**: PLAN.md's M2 card line lists "capabilities (vision/tool/reasoning)", but only **tool** has a first-class FL flag. Honesty (Constitution IV) requires deriving vision from modalities and reasoning only from FL's declared `Capabilities` string, and rendering "unknown" where the source is null — never asserting a capability the SDK does not back.

**Alternatives considered**: Inferring capabilities from alias substrings (e.g. "vision", "r1") — rejected as fabrication.

---

## R3 — Curated default view (FR-008/FR-009, US4)

**Decision**: The curated default is a **deterministic, app-defined allow-list of aliases** (a `CuratedCatalog` constant set in Core, e.g. a short recommended/popular subset such as the small Phi/Qwen instruct models known to be in the FL catalog), applied as a pure-logic selection (`CuratedSelector.Select(models)`) that:
1. Surfaces only models whose alias is in the curated set, preserving a deterministic order.
2. Is clearly labeled "Recommended" / "Curated" (distinct from "All models") with a single discoverable toggle to the full catalog.
3. Makes **no** fabricated quality claim — the label is "curated by FoundryForge", not "best" or "fastest".
4. Is bypassed entirely by any active search/filter, which always run over the **full** catalog (FR-009).

**Rationale**: DEC-014 positioning reframes the curated catalog as a *feature* (trust/compliance), not a limitation. A static deterministic allow-list is honest, testable without a dylib (pure-logic seam, FR-019), and avoids fabricated "recommended" ranking the FL metadata cannot support. If a curated alias is absent from the live catalog it is simply skipped (the curated view degrades gracefully, never errors).

**Alternatives considered**:
- *Rank by FileSizeMb / popularity* — rejected: FL exposes no popularity metric; size-ranking implies a quality claim.
- *Curate by Publisher == Microsoft* — viable secondary heuristic but less predictable; kept the explicit alias allow-list as primary for determinism and reviewability.

---

## R4 — Filter facets & "unknown" handling (FR-006, US3)

**Decision**: Reuse the existing `CatalogFilter` (`Device`/`Task`/`Provider`/`SearchText`/`CachedOnly`) and `CatalogFilterExtensions.Matches` **unchanged in behavior**. Facet option lists (Task, Provider) are derived from the enriched catalog's distinct non-empty values; models whose facet is genuinely unknown are **excluded from a real facet bucket** and are simply not matched when that facet is selected (FR-006 #6 / US3 AC-6) — never relabeled into a known value. Device facet stays the fixed CPU/GPU/NPU enum; `Invalid` device models match no device filter.

**Rationale**: The M1 filter seam is already unit-tested and proven (31/31, DEC-016). M2 is UI wiring plus honest facet derivation; no new filtering logic (FR-005, FR-007 — no duplicate predicate). One small extension may be needed: `CatalogFilterExtensions` already matches a `Device` against `model.Variants` — that continues to work because variants now carry real `Device` values.

**Open extension**: if device/task/provider facet derivation needs a helper, it lives in Core as a pure function (`CatalogFacets.Derive(models)`), dylib-free testable.

---

## R5 — RAM-fit indicator (stretch, optional)

**Decision**: Out of core M2 scope; **optional honest stretch only**. The `RamFitHeuristic`/`RamFitResult` pure-logic seam already exists (M1). If surfaced at all, the card shows an honest "size vs *free* RAM" hint with the "long chats use more" caveat and **never** a confident green "will run" verdict (PLAN.md M2 note; constitution capability honesty). Core M2 acceptance does not depend on it; it is not wired unless time permits and free-RAM sourcing is honest.

**Rationale**: PLAN.md explicitly rates the memory-fit badge as a *size-vs-free-RAM indicator, not a confident verdict*; the spec Assumptions mark it stretch. Keeping it optional protects the milestone from scope creep (Constitution scope gate).

---

## R6 — Screenshot evidence path (KI-001) — re-verify during M2

**Decision**: Treat `maui devflow ui screenshot` as an **available evidence path to re-verify at M2 close**, not a blocked one. David confirms the command works; the M0-era blank WebView capture (KI-001) may have been misuse/timing. The M2 verification protocol is:
1. **Primary (always valid)**: DevFlow DOM inspection — `maui devflow webview source`, `Runtime evaluate "document.querySelectorAll('[data-testid]')"`, computed-style/contrast queries — driven against stable `id`/`data-testid` hooks on the catalog list, cards, badges, search, filters, and state regions.
2. **Screenshot re-verification**: run `maui devflow ui screenshot` against the live catalog at M2 close. **If it captures the WebView**, attach screenshots as the evidence David asked for and update KI-001 to "upstream-fixed-pending-removal". **If it still returns blank**, fall back to DOM snapshots + a non-interactive native window capture, keep KI-001 open, and record the re-verified-still-blocked result honestly in the closing note.
3. Either way the closing note ends with a `Verified:` line (Constitution II, FR-023) naming the checks that ran.

**Rationale**: Constitution II requires an end-to-end DevFlow check; KI-001 currently routes WebView visual proof through DOM. The user instruction is explicit: re-verify the screenshot path in M2 and treat it as available-not-blocked. This decision records both branches so the milestone cannot stall on the outcome.

**Alternatives considered**: Relying solely on DOM (M1's path) — still the guaranteed fallback, but we actively re-test the screenshot path per the user's request rather than assuming it is blocked.

---

## R7 — Discovery is strictly non-mutating (FR-013, SC-008)

**Decision**: The catalog UI calls **only** the read paths of `IFoundryCatalogService` — `BrowseAsync`, `ListCachedAsync`, `ListLoadedAsync`, `GetModelAsync`, `GetVariantsAsync`. It never calls `DownloadAsync`/`LoadAsync`/`UnloadAsync`/`DeleteFromCacheAsync`, and renders **zero** affordances for them. Manual refresh (FR-017) re-invokes `BrowseAsync`/`ListCachedAsync` only. `IModel.Variants`/`ModelInfo` reads do not trigger downloads (metadata is already materialized by `ListModelsAsync`).

**Rationale**: M2 is browse/discovery only; the mutation methods exist on the seam (M1) but are M3/M4 surface. A read-only consumer guarantees SC-008 (0 FL download/load operations across a browse session).

---

## R8 — KI-008 dispose hardening (FR-022)

**Decision**: KI-008 (M1 lifecycle dispose-race nits, explicitly assigned to "M2 hardening") is **in-scope as a small, isolated hardening task**: volatile `_disposed`, dispose under the init lock, continuation-dispose for a late-completing init. If it cannot be safely completed within M2, the deferral is recorded honestly in the closing note (FR-022) and KI-008 stays open. This is the only change permitted outside the catalog surface, and it is a pre-identified, scoped follow-up (Constitution III — cleaning up our own mess, traceable to KI-008).

**Rationale**: Spec FR-022 and SC-012 require KI-008 to be either completed or its deferral recorded. Keeping it isolated preserves surgical-change discipline.

---

## Resolved unknowns summary

| Unknown | Resolution |
|---------|------------|
| Which rich fields are real in FL 1.2.3? | R1 — reflected surface; size/device/EP/task/provider/context/license/variants/tool real; vision derived; reasoning only if FL declares it |
| How to show fields FL omits? | R1/R2 — nullable DTO + explicit "unknown / not provided"; never `0 GB`/defaulted |
| Curated default rule? | R3 — deterministic app-defined alias allow-list, labeled, no quality claim |
| New filtering logic needed? | R4 — no; reuse `CatalogFilter`/`CatalogFilterExtensions`; add pure facet/curated helpers in Core |
| RAM-fit? | R5 — optional honest stretch, not core M2 |
| Screenshot evidence? | R6 — re-verify `ui screenshot` at close; DOM is the guaranteed fallback; record outcome |
| Mutation risk while browsing? | R7 — read-only seam usage, zero mutation affordances |
| KI-008? | R8 — scoped hardening in M2 or honest deferral |

All `NEEDS CLARIFICATION` resolved. Proceed to Phase 1.
