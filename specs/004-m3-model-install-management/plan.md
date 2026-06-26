# Implementation Plan: M3 — Model Install & Management

**Branch**: `004-m3-model-install-management` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-m3-model-install-management/spec.md`

## Summary

M3 turns the M2 **browse-only** catalog into an **actionable model manager**. From a model card the user can: download a model with live percent progress + cancel + optional auto-load (US1); load/unload through the M1 `IModelStateGate` with a currently-loaded indicator reflected on cards (US2); delete a cached model **only** behind an explicit, model-naming, disk-freeing confirmation with a do-nothing cancel path (US3, the Constitution IV load-bearing story); see cached-vs-available groups computed from the authoritative cached source — resolving KI-009 (US4); pin a specific variant honored by download/load (US5); see a non-blocking disk-fit warning before download (US6); and view/change the model cache directory without silently moving or wiping the existing cache (US7). BYOM ONNX import is an **optional P3 stretch** (US8) that does not gate M3.

**Technical approach**: M3 is overwhelmingly a **UI + pure-logic-seam** milestone. The service surface (`IFoundryCatalogService`, `ISettingsService`) already exposes everything the P1/P2 stories need; the only additive seam is variant targeting (a back-compatible optional `variantId` on download/load — see research R7). New work lands as: (a) **Core pure seams** — `DiskFitHeuristic`, `CatalogGrouping`, `VariantSelectionState` — dylib-free and unit-tested mirroring the existing `RamFitHeuristic` precedent; (b) **Blazor UI** — management actions + download-progress + a `ConfirmDialog` on `ModelCard`, a cached/available grouped catalog on `Home.razor`, and a `Settings` surface for the cache directory; (c) **Foundry layer fixes** — KI-009 cached-source trust, download-progress wiring (already present), cancellation pass-through, and variant targeting. Verification follows Constitution II: dylib-free unit tests for every seam (including the consent gate) + a real Apple-Silicon DevFlow DOM e2e whose only live delete is on a model the test itself downloaded, leaving pre-existing user cache untouched (FR-036).

## Technical Context

**Language/Version**: C# 13 / .NET 11 (`net11.0-macos`, pinned via `global.json` / `Directory.Packages.props`).

**Primary Dependencies**: .NET MAUI Blazor Hybrid (AppKit head); Microsoft.AI.Foundry.Local `1.2.3` (consumed only behind the Foundry layer); Microsoft.Maui.DevFlow Agent/Blazor `0.1.0-preview.8.26256.5` (KI-002) for DOM verification.

**Storage**: Model cache (multi-GB, on disk under `AppSettings.ModelCacheDirectory`, managed by Foundry Local) — **protected user data** (Constitution IV). Settings: human-readable JSON via `FileSettingsService` (consent-gated). No new persistent store in M3; download/operation state is transient UI view-model state.

**Testing**: xUnit in `tests/FoundryStudio.Tests` (dylib-free unit tests for all Core seams + the consent gate); MAUI DevFlow DOM inspection on real Apple Silicon for UI verification (KI-001 sanctioned evidence path).

**Target Platform**: macOS / Apple Silicon only (DEC-004/016/017). No iOS/Android/Mac Catalyst. ONNX-only models; no GGUF/safetensors.

**Project Type**: Desktop app — single MAUI Blazor Hybrid solution of 4 existing projects (App, Core, Foundry, Tests). No new projects.

**Performance Goals**: UI responsive during download (progress marshaled to UI thread via `InvokeAsync(StateHasChanged)`, never blocking the BlazorWebView dispatcher — KI-005). Download progress reflects real callback cadence; no fabricated animation. Load/unload/delete never tear an in-flight generation (gate-serialized).

**Constraints**: Capability honesty (Constitution IV): real progress only; honest "unknown" for null size/unknowable disk fit; no GGUF import; no inference-param controls (M4). Data preservation (Constitution IV): no destructive default, explicit per-action consent, no silent cache move/wipe. Layering (Constitution V / DEC-004): UI consumes only `IFoundryCatalogService` / `ISettingsService` + Core seams, never the FL SDK. Concurrency (Constitution V): all mutations through the single `IModelStateGate`; `ModelBusyException` surfaced honestly. Accessibility: WCAG AA on every new control. Off-dispatcher init discipline (KI-005): no blocking FL calls on the UI thread.

**Scale/Scope**: 7 in-scope user stories (3×P1, 4×P2) + 1 optional P3; ~3 new Core seams, ~3–4 new/extended Blazor components, 1 new settings page, KI-009 fix + variant-targeting delta in the Foundry layer; ~6 new unit test classes. No new service projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Evidence / How M3 complies |
|-----------|--------|----------------------------|
| **I. Citation Before Action** | **PASS** | Every design claim cites real code (`FoundryCatalogService.cs` L82-136 download/load/unload/delete; `ModelStateGate.cs`; `RamFitHeuristic.cs` precedent), the FL SDK reflected signature (research R1: `IModel.DownloadAsync(Action<float>, CancellationToken?)`, `SelectVariant(IModel)`), `docs/PLAN.md` M3 lines 104-110, `KNOWN-ISSUES.md` (KI-001/005/009), and `.squad/decisions.md` (DEC-004/016/017). Open runtime unknowns (FL cancel honoring) are flagged in research.md, not guessed. |
| **II. Pre-Completion Verification (NON-NEGOTIABLE)** | **PASS (planned)** | quickstart.md defines a real Apple-Silicon DevFlow DOM e2e (download → load → unload → consent-gated delete) + dylib-free unit tests for every seam and the consent gate. M3 closes with a `Verified:` line; reviewer independent of author (FR-035). DOM is the sanctioned autonomous evidence path (KI-001). |
| **III. Surgical Changes & Reviewer Independence** | **PASS** | Scope is additive: new Core seams, new/extended UI, a bounded KI-009 + variant delta in the Foundry layer. No opportunistic refactor of M2 browse code. KI-009 fix is explicitly mandated by FR-017 (traces to request). Author does not self-approve. |
| **IV. Data Preservation & Capability Honesty** | **PASS (load-bearing)** | Delete requires explicit in-UI confirmation naming the model + stating disk is freed; `DeleteFromCacheAsync(userConfirmed:false)` already throws (the enforcement point) and is unit-proven dylib-free (SC-006). No one-click destructive path; cancel = no-op. Cache-dir change never silently moves/wipes (FR-026) — warn/confirm + invalid-dir rejection. Capability honesty: real progress only, honest "unknown" for size/disk, no GGUF, no inference params (M4). Verification never wipes pre-existing user cache (FR-036). |
| **V. Native-Load & In-Process Discipline** | **PASS** | UI consumes only Core abstractions; FL SDK stays behind the Foundry layer (FR-031). All mutations route through the single `IModelStateGate` (one manager, drain/reject). Progress marshaled off the dispatcher with `InvokeAsync(StateHasChanged)` (KI-005). No second manager, no loopback. |

**Initial gate**: ✅ PASS (no violations). **Post-Design re-check (Phase 1)**: ✅ PASS — the additive variant-targeting seam (R7) is a back-compatible optional parameter on the existing interface, not a new service or layer breach; no Complexity Tracking entry required.

## Project Structure

### Documentation (this feature)

```text
specs/004-m3-model-install-management/
├── plan.md              # This file
├── research.md          # Phase 0 — FL cancel support, disk approach, no-wipe cache-dir, KI-009, variant honoring, evidence
├── data-model.md        # Phase 1 — download/op state, DiskFit result, grouping/selection view state, confirm-dialog state
├── quickstart.md        # Phase 1 — DevFlow e2e + data-preservation-safe verification plan
├── contracts/           # Phase 1
│   ├── management-ui.dom.md      # DOM hooks for management actions + negative/consent invariants
│   ├── core-seams.md             # DiskFitHeuristic, CatalogGrouping, VariantSelectionState (+ consent gate coverage)
│   └── service-surface.md        # Existing surface relied on + the additive variant-targeting delta
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root) — extend the existing 4 projects, no new projects

```text
src/FoundryStudio.Core/                      # FL-free, dylib-free
├── Catalog/
│   ├── DiskFitHeuristic.cs        # NEW — SizeGb + safety margin vs free disk → fits | warn | unknown (mirrors RamFitHeuristic)
│   ├── CatalogGrouping.cs         # NEW — partition ModelInfo into Cached / Available from authoritative cached source (KI-009)
│   ├── RamFitHeuristic.cs         # (existing precedent)
│   └── CuratedSelector.cs / CatalogFacets.cs / CatalogFilterExtensions.cs / CapabilityParser.cs   # (existing)
├── Catalog/VariantSelectionState.cs   # NEW — pure pinned-variant state: default, pin, no-variants case
├── Models/
│   ├── DiskFitResult.cs           # NEW — record { DiskFit Fit, double? MarginGb } + enum { Fits, Warn, Unknown }
│   ├── ModelInfo.cs / ModelVariant.cs / AppSettings.cs / RamFitResult.cs   # (existing; no schema change for P1/P2)
│   └── …
└── Abstractions/
    ├── IFoundryCatalogService.cs  # CHANGED — additive optional `variantId` on DownloadAsync/LoadAsync (R7, back-compatible)
    └── ISettingsService.cs        # (existing; unchanged)

src/FoundryStudio.Foundry/                   # FL behind the seam
├── FoundryCatalogService.cs       # CHANGED — KI-009 cached-source trust; variant targeting via IModel.SelectVariant; cancel pass-through (progress already wired)
└── FileSettingsService.cs         # (existing; cache-dir update already consent-gated)

src/FoundryStudio.App/Components/
├── Catalog/
│   ├── ModelCard.razor            # CHANGED — management actions (download/cancel/auto-load, load/unload + loaded indicator, delete, variant select, disk-fit note)
│   ├── ConfirmDialog.razor        # NEW — reusable model-naming consent dialog (delete + cache-dir change)
│   ├── DownloadProgress.razor     # NEW — real-percent progress + cancel (indeterminate when no progress source)
│   ├── CatalogViewState.cs        # CHANGED — grouped (cached/available) view + loaded set + per-card op state
│   └── ModelOperationState.cs     # NEW — transient per-model op state (idle/downloading/cancelling/failed) + auto-load choice
├── Pages/
│   ├── Home.razor                 # CHANGED — render cached/available groups; refresh loaded state after mutations
│   └── Settings.razor             # NEW — view/change ModelCacheDirectory via ISettingsService (warn/confirm, invalid-dir reject)
└── (BYOM import flow — OPTIONAL P3, only if implemented)

tests/FoundryStudio.Tests/
├── DiskFitHeuristicTests.cs       # NEW — fits/warn/unknown matrix incl. null SizeGb (SC-009)
├── CatalogGroupingTests.cs        # NEW — exactly-one-group partition; KI-009 cached-source trust (SC-007)
├── VariantSelectionStateTests.cs  # NEW — default/pin/no-variants (SC-008)
├── DeleteConsentGateTests.cs      # NEW — DeleteFromCacheAsync(userConfirmed:false) removes nothing (SC-006, dylib-free)
└── (existing tests — RamFitHeuristicTests, ModelStateGateTests, SettingsDocumentTests, …)
```

**Structure Decision**: Single MAUI Blazor Hybrid desktop solution; extend the four existing projects (`FoundryStudio.App`, `.Core`, `.Foundry`, `.Tests`). Pure logic lives in `.Core` (dylib-free, unit-tested), FL access stays in `.Foundry`, UI in `.App` consuming only Core abstractions — preserving the DEC-004 / Constitution V layering. No new projects (Complexity Tracking not triggered).

## Complexity Tracking

> No Constitution Check violations. No entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | — | — |
