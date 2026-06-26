# Implementation Plan: M2 — Catalog Browse + Discovery

**Branch**: `003-m2-catalog-browse-discovery` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-m2-catalog-browse-discovery/spec.md`

## Summary

M2 builds the first real end-user screen of FoundryStudio on the M1 foundation (DEC-016): a browsable, searchable, filterable catalog of Foundry Local models rendered as informative, honest per-model cards, surfaced only after the M1 ready-gate reaches "ready". It is **discovery only** — zero download/load/delete/variant-select/chat affordances; browsing triggers no FL mutation (FR-013, R7). M2 replaces the M1 `MapBasic`/`GetVariantsAsync` stubs (`TODO(M2)`) with **real** enriched metadata mapped from the reflected Foundry Local `1.2.3` surface (`ModelInfo`/`Runtime`/`IModel.Variants` — research.md R1), rendering an explicit "unknown / not provided" wherever FL genuinely omits a value (Constitution IV). All model data flows through the FL-free `IFoundryCatalogService` seam; the UI never touches the FL SDK (Constitution V / DEC-004). Filter/search reuse the existing tested `CatalogFilter`/`CatalogFilterExtensions` pure-logic seam; new pure helpers (`CapabilityParser`, `CuratedSelector`, `CatalogFacets`) stay dylib-free testable in Core. Verification closes with a real Apple-Silicon DevFlow end-to-end check (DOM primary per KI-001; `ui screenshot` re-tested per R6) + unit tests green + CI clean + a `Verified:` line.

## Technical Context

**Language/Version**: C# / .NET 11 (`net11.0` for Core/Foundry/Tests; `net11.0-macos` for the App AppKit head) — DEC-016.

**Primary Dependencies**: .NET MAUI (maui-labs AppKit head) + Blazor Hybrid (BlazorWebView); `Microsoft.AI.Foundry.Local` 1.2.3 (Foundry project only, behind the seam); `Microsoft.Extensions.AI`; DevFlow tooling (DEBUG). No new packages, no new projects.

**Storage**: None new. Transient in-memory catalog per browse session; FL's own ~6h catalog cache is upstream/untouched. Settings remain the M1 `FileSettingsService` (unused by M2).

**Testing**: xUnit in `tests/FoundryStudio.Tests` (Core-only, dylib-free). New tests: `CapabilityParser`, `CuratedSelector`, `CatalogFacets`, FL→Core pure mapping transform, updated `CatalogFilter` null-device case. Plus real-hardware DevFlow DOM verification (quickstart V1–V8).

**Target Platform**: macOS / Apple Silicon only (DEC-004), AppKit head.

**Project Type**: Desktop app (single solution, existing 4 projects: Core / Foundry / App / Tests).

**Performance Goals**: Catalog list (~46 models, DEC-011) renders responsively after ready; search/filter narrowing stays interactive; no model content fetched/downloaded to browse (FR-013).

**Constraints**: WCAG AA by default (FR-018); capability honesty — no fabricated metadata, no UI for unsupported FL features (Constitution IV); UI never references the FL SDK (Constitution V); off-dispatcher init preserved (KI-005); `_Imports` already correct (KI-006); KI-001 DOM-first verification; discovery strictly non-mutating (FR-013).

**Scale/Scope**: One catalog page + per-model card + search + 3 facet filters + cached-only toggle + curated/full toggle + 3 honest states; metadata enrichment in the Foundry mapper; 3 new Core pure helpers + DTO extensions; ~5 new unit test classes.

## Constitution Check

*GATE: evaluated against all five principles of constitution v1.0.0. Re-checked after Phase 1 design.*

| Principle | Assessment | Verdict |
|-----------|-----------|---------|
| **I. Citation Before Action** | The load-bearing decision (which FL metadata is real) is cited from the reflected `Microsoft.AI.Foundry.Local` 1.2.3 assembly (research.md R1), not guessed; every mapping field traces to a concrete FL property; scope/positioning cite docs/PLAN.md, DEC-004/014/016, KI-001/006/008, and the M1 source. | **PASS** |
| **II. Pre-Completion Verification (NON-NEGOTIABLE)** | quickstart V1–V8 define a real Apple-Silicon DevFlow end-to-end browse/search/filter check (DOM primary per KI-001; `ui screenshot` re-tested per R6); FR-023 mandates the closing `Verified:` line; build+dylib-free unit tests are prerequisites, not the verification. | **PASS** |
| **III. Surgical Changes & Reviewer Independence** | Every change traces to an FR; the only out-of-catalog edit is the pre-identified KI-008 dispose hardening (FR-022, R8). No opportunistic refactor. FR-024/SC-011 require an independent approver (author ≠ sole approver). | **PASS** |
| **IV. Data Preservation & Capability Honesty** | M2 is read-only discovery — no cache/settings/history mutation; honest "unknown / not provided" for every absent FL field (never `0 GB`/defaulted); capabilities shown only where FL declares them (R2); no UI for unsupported FL features (FR-020). | **PASS** |
| **V. Native-Load & In-Process Discipline** | UI consumes only the FL-free `IFoundryCatalogService`; FL types confined to the Foundry project; one `FoundryLocalManager` singleton via the M1 ready-gate (`AppReadyBoundary`); no second manager, no loopback; off-dispatcher init preserved (KI-005). | **PASS** |

**Gate result: PASS (all five).** No violations. Complexity Tracking is intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/003-m2-catalog-browse-discovery/
├── plan.md              # This file
├── research.md          # Phase 0 — FL metadata-availability (R1) + screenshot-evidence (R6) decisions
├── data-model.md        # Phase 1 — enriched ModelInfo/ModelVariant/ModelCapabilities + CatalogViewState
├── quickstart.md        # Phase 1 — e2e DevFlow validation V1–V8 (+ screenshot re-test)
├── contracts/
│   ├── IFoundryCatalogService.read.md   # read-only seam usage + enrichment guarantees
│   ├── core-seams.md                    # CapabilityParser / CuratedSelector / CatalogFacets contracts
│   └── catalog-ui.dom.md                # stable id/data-testid DOM + a11y contract for DevFlow
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root) — build on the existing 4 projects (no new projects)

```text
src/FoundryStudio.Core/                       # FL-free DTOs + pure-logic seams
├── Models/
│   ├── ModelInfo.cs                          # EXTEND: SizeGb→double?, Device→Device?, + EP/Context/MaxOut/License/Publisher/ModelType/Capabilities
│   ├── ModelVariant.cs                        # EXTEND: Device→Device?, SizeGb→double?
│   ├── ModelCapabilities.cs                   # NEW: Vision/ToolCalling/Reasoning/ToolCallingKnown
│   └── CatalogFilter.cs                       # UNCHANGED (FR-005/FR-007)
├── Catalog/
│   ├── CatalogFilterExtensions.cs             # MINIMAL: null-device safety only (behavior preserved)
│   ├── CapabilityParser.cs                    # NEW: honest capability derivation (R2)
│   ├── CuratedSelector.cs                     # NEW: deterministic curated allow-list (R3)
│   ├── CatalogFacets.cs                       # NEW: honest Task/Provider/Device facet derivation (R4)
│   └── RamFitHeuristic.cs                     # UNCHANGED (R5 optional)
└── Abstractions/IFoundryCatalogService.cs     # UNCHANGED interface (read methods used by M2)

src/FoundryStudio.Foundry/
└── FoundryCatalogService.cs                   # ENRICH: MapBasic→MapEnriched (FL ModelInfo/Runtime/Variants→Core); GetVariantsAsync real mapping (resolve TODO(M2))
    # + FoundryLifecycle.cs                    # OPTIONAL: KI-008 dispose hardening (FR-022/R8)

src/FoundryStudio.App/Components/
├── Pages/
│   └── Catalog.razor                          # NEW: the catalog page (route "/"; replaces Home smoke as the landing surface)
├── Catalog/                                   # NEW component folder
│   ├── CatalogList.razor                      # list + states (loading/empty/error) orchestration
│   ├── ModelCard.razor                        # per-model card (alias/size/device-EP/context/caps/license/cached badge/variants)
│   ├── CatalogFilters.razor                   # search + device/task/provider + cached-only + curated/full + reset
│   └── CatalogViewState.cs                    # view-model (Status/AllModels/Filter/ViewMode/Visible/Facets)
├── Pages/Home.razor                           # DEMOTE/REMOVE the M1 chat-smoke landing (Catalog becomes "/")
└── wwwroot/app.css                            # EXTEND: catalog/card/badge/filter styles (AA contrast, no color-only)

tests/FoundryStudio.Tests/                     # Core-only, dylib-free
├── CapabilityParserTests.cs                   # NEW (R2)
├── CuratedSelectorTests.cs                    # NEW (R3)
├── CatalogFacetsTests.cs                      # NEW (R4)
├── ModelInfoMappingTests.cs                   # NEW: FL-metadata fixtures → Core (FR-019, pure transform)
└── CatalogFilterTests.cs                      # EXTEND: null-device case
```

**Structure Decision**: Single existing solution, four existing projects (Core / Foundry / App / Tests) — no new projects (per the task's PROJECT STRUCTURE constraint). FL-free DTOs and all pure-logic seams live in `FoundryStudio.Core` (dylib-free testable, FR-019); FL-touching enrichment lives in `FoundryStudio.Foundry` (`FoundryCatalogService.MapEnriched`), with the *post-FL pure transform* (capability parse, size conversion, unknown handling) delegated to Core helpers so it is fixture-testable without a dylib; the catalog UI lives under `src/FoundryStudio.App/Components/` (a `Pages/Catalog.razor` + a `Catalog/` component folder) with stable `id`/`data-testid` hooks for DevFlow DOM verification (contracts/catalog-ui.dom.md). The CI seam gate (lock files, Core-only test project) stays green.

## Complexity Tracking

> No Constitution Check violations. No entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_  | _(n/a)_    | _(n/a)_                             |
