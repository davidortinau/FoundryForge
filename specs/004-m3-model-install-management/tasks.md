---
description: "Task list for M3 — Model Install & Management"
---

# Tasks: M3 — Model Install & Management

**Input**: Design documents from `/specs/004-m3-model-install-management/`

**Prerequisites**: plan.md (Constitution PASS; concrete file layout), spec.md (8 user stories P1–P3, 36 FRs, 12 SCs), research.md (FL cancel/DiskFit/no-wipe/KI-009/variant/KI-001), data-model.md (DiskFitResult, VariantSelectionState, CatalogGrouping, ModelOperationState, DeleteConfirmation, SettingsViewState), contracts/ (management-ui.dom.md, core-seams.md, service-surface.md), quickstart.md (DevFlow DOM e2e + data-preservation-safe verification)

**Tests**: M3 ships **dylib-free xUnit** for the four pure seams (DiskFitHeuristic, CatalogGrouping, VariantSelectionState, the Delete consent gate) + a settings no-wipe seam test. All UI behavior is verified via **DevFlow DOM inspection** (KI-001 — the WebView pixel screenshot needs the window frontmost, so DOM is the sanctioned autonomous evidence path) in the Polish/close phase, not via per-story UI unit tests.

**Organization**: Tasks are grouped by user story (priority order: 3×P1, 4×P2, 1×optional-P3) so each story can be implemented and DevFlow-verified independently.

## M3 Guardrails (apply to EVERY task)

- **Data preservation (Constitution IV, load-bearing)**: Delete requires an explicit in-UI confirmation naming the exact model + its disk-freeing consequence; a single Delete activation only *opens* the dialog (never deletes); cancel = no-op; `DeleteFromCacheAsync(alias, userConfirmed:true, ct)` is the enforcement point and throws on `false`. Cache-dir change never silently moves/wipes the existing cache. Any live destructive delete in verification runs **only** on a model the test itself downloaded — **never** pre-existing user cache (FR-036).
- **Capability honesty (Constitution IV)**: real `IProgress<double>` progress only (no fabricated/animated-only bar); honest "unknown" for null size / unknowable disk fit / no variants; no GGUF import (ONNX-only); no inference-parameter controls (M4).
- **Layering (Constitution V / DEC-004)**: `FoundryForge.App` consumes **only** `IFoundryCatalogService`, `ISettingsService`, and `FoundryForge.Core` seams — **never** `Microsoft.AI.Foundry.Local` (FR-031). All FL types stay in `FoundryForge.Foundry`.
- **Concurrency (Constitution V)**: all mutations route through the single `IModelStateGate`; `ModelBusyException` surfaced honestly; progress marshaled off the dispatcher via `InvokeAsync(StateHasChanged)` (KI-005).
- **Seam purity**: the four pure seams stay FL-free and dylib-free so the CI seam gate stays green; update `packages.lock.json` only if the package set changes (it should not).
- **Accessibility (FR-033)**: every new control labeled, keyboard-reachable, state changes perceivable, never color-alone; WCAG AA.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: maps the task to its user story (US1–US8). Setup / Foundational / Polish carry no story label.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the M3 baseline before any code changes.

- [ ] T001 Confirm a green baseline: build the solution and run `dotnet test tests/FoundryForge.Tests/FoundryForge.Tests.csproj` from repo root; record the existing-suite pass set (`RamFitHeuristicTests`, `ModelStateGateTests`, `SettingsDocumentTests`, `CapabilityParserTests`, `CuratedSelectorTests`, `CatalogFacetsTests`, …) so regressions are visible.
- [ ] T002 [P] Verify M3 adds **no new packages**: confirm `Directory.Packages.props` and each project's `packages.lock.json` are unchanged by M3 scope (UI + pure seams only); note that lock-files are regenerated **only** if the package set actually changes.

**Checkpoint**: Baseline green; package set frozen.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: DTOs / seams / service deltas that multiple UI stories consume. These MUST land before the story phases so US1–US7 can be staffed in parallel.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [ ] T003 Add the back-compatible additive `variantId` optional parameter to `DownloadAsync` and `LoadAsync` in `src/FoundryForge.Core/Abstractions/IFoundryCatalogService.cs` — `Task DownloadAsync(string alias, IProgress<double>? progress = null, string? variantId = null, CancellationToken ct = default)` and `Task LoadAsync(string alias, string? variantId = null, CancellationToken ct = default)` (R7). All existing M1/M2 callers MUST compile unchanged; do **not** add any new method or interface.
- [ ] T004 Thread the new `variantId` parameter through `src/FoundryForge.Foundry/FoundryCatalogService.cs` `DownloadAsync`/`LoadAsync` as a no-op when `null` (identical to today's behavior); leave the actual `SelectVariant` honoring to US5 (T031). Keep existing Drain gating, progress wiring (`p => progress?.Report(p)`), and cancellation pass-through (`ct`) intact (depends on T003).
- [ ] T005 [P] Create `src/FoundryForge.App/Components/Catalog/ModelOperationState.cs` — transient per-model view state: `enum ModelOpPhase { Idle, Downloading, Cancelling, Loading, Unloading, Deleting, Failed, Busy }`, `ModelOpPhase Phase`, `double? ProgressPercent` (null ⇒ indeterminate, honest FR-002), `bool AutoLoadAfterDownload`, `string? ErrorMessage`, `CancellationTokenSource? Cts`. Consumed by US1/US2/US3.
- [ ] T006 [P] Create reusable `src/FoundryForge.App/Components/Catalog/ConfirmDialog.razor` — model-naming consent dialog with DOM hooks `data-testid="confirm-dialog"` (`role="dialog"` `aria-modal="true"`, hidden until activated, focus-trapped, Esc = cancel), `data-testid="confirm-message"` (names the exact model + consequence text), `data-testid="confirm-accept"`, `data-testid="confirm-cancel"` (no-op). Parameterized so US3 (delete) and US7 (cache-dir change) both reuse it; no destructive default (FR-010/012/014).
- [ ] T007 Extend `src/FoundryForge.App/Components/Catalog/CatalogViewState.cs` (additive, surgical — do **not** touch M2 filter/facet/curated logic): add a grouped (Cached / Available) projection slot, the authoritative **loaded set**, and a per-alias `ModelOperationState` map, plus a single recompute-on-mutation entry point that re-reads `ListCachedAsync`/`ListLoadedAsync` (never optimistic, R6). US4 (T028) wires the actual `CatalogGrouping.Partition` call into the grouped slot (depends on T005).

**Checkpoint**: Foundation ready — US1–US7 can now proceed in parallel.

---

## Phase 3: User Story 1 - Download with live progress, cancel, optional auto-load (Priority: P1) 🎯 MVP

**Goal**: From a not-cached card, start a download with real percent progress, cancel an in-flight download leaving the model not-cached, and optionally auto-load on completion.

**Independent Test (DevFlow DOM)**: On Apple Silicon, pick a small not-cached model the test selects (e.g. `qwen2.5-0.5b`); activate `card-download`; assert `card-download-progress` (`role="progressbar"`, `aria-valuenow`) advances from the **real** `IProgress<double>` callback (not fabricated); activate `card-download-cancel` and assert the card returns to not-cached/idle and the model is **absent** from `ListCachedAsync`; with `card-autoload` checked, assert a completed download transitions to loaded.

- [ ] T008 [P] [US1] Create `src/FoundryForge.App/Components/Catalog/DownloadProgress.razor` — real-percent progress + cancel control: `data-testid="card-download-progress"` with `role="progressbar"` + `aria-valuenow`/`aria-valuemin`/`aria-valuemax`, falling back to an **indeterminate** `aria-busy` state when `ProgressPercent` is null (no progress source — honest, FR-002); `data-testid="card-download-cancel"` bound to `CancellationTokenSource.Cancel()` (FR-003).
- [ ] T009 [US1] In `src/FoundryForge.App/Components/Catalog/ModelCard.razor`, add the download affordances: `data-testid="card-download"` button wired to `IFoundryCatalogService.DownloadAsync(alias, progress, variantId, ct)`; `data-testid="card-autoload"` checkbox (+ `<label>`) bound to `ModelOperationState.AutoLoadAfterDownload`; `data-testid="card-download-error"` (`role="alert"`) for an honest diagnosed failure that returns the card to not-cached (FR-005). Render `DownloadProgress` while `Phase == Downloading` (depends on T005, T008).
- [ ] T010 [US1] Wire the download lifecycle in `ModelCard.razor`/`CatalogViewState`: create the `CancellationTokenSource`, report progress via an `IProgress<double>` whose handler marshals with `InvokeAsync(StateHasChanged)` (KI-005, never blocking the BlazorWebView dispatcher); on cancel transition Downloading → Cancelling → Idle and re-check `ListCachedAsync` so **no partial download is shown as cached** (FR-003, R1); on success with auto-load checked, call `LoadAsync` through the gate then refresh; resolve the cancel-races-completion edge to a single coherent state (depends on T007, T009).
- [ ] T011 [US1] In `src/FoundryForge.App/Components/Pages/Home.razor`, refresh the authoritative cached/loaded sets after a download completes, fails, or is cancelled so the card and groups reflect real state (refresh-on-mutation, FR-018) (depends on T007).

**Checkpoint**: US1 independently DevFlow-verifiable (SC-001/002/003).

---

## Phase 4: User Story 2 - Load / unload with a currently-loaded indicator (Priority: P1)

**Goal**: Load/unload a cached model through the M1 gate, show a currently-loaded indicator, reflect loaded state on all cards, and surface busy/at-capacity honestly.

**Independent Test (DevFlow DOM)**: With a cached model, activate `card-load`; assert `card-loaded-badge` + app-wide `loaded-indicator` appear; activate `card-unload` and assert they clear; force concurrent contention and assert an honest `card-busy` (`ModelBusyException`) — no hang/crash; all cards reflect current loaded state with no stale value.

- [ ] T012 [US2] In `src/FoundryForge.App/Components/Catalog/ModelCard.razor`, add `data-testid="card-load"` → `LoadAsync(alias, variantId, ct)` (gate) and `data-testid="card-unload"` → `UnloadAsync(alias, ct)` (gate), and `data-testid="card-loaded-badge"` derived from the authoritative loaded set (text + icon, never color-alone) (FR-006/007) (depends on T005, T007).
- [ ] T013 [US2] Add the app-wide `data-testid="loaded-indicator"` (in `Home.razor` or the shared layout) sourced from `ListLoadedAsync`, naming the loaded model(s) or "none loaded"; recompute on every load/unload mutation so all cards reflect current state with no stale value (FR-007, R6) (depends on T007).
- [ ] T014 [US2] Surface honest busy/limit feedback: `data-testid="card-busy"` (`role="alert"`) rendering the gate's `ModelBusyException` ("model is busy, try again") and the FL at-capacity case (state what to unload first — do not silently no-op) (FR-008/009). The loaded-model limit comes from Foundry Local; surface it honestly rather than asserting a fixed number (depends on T012).

**Checkpoint**: US1 + US2 = "install and make ready"; both independently DevFlow-verifiable (SC-004).

---

## Phase 5: User Story 3 - Consent-gated delete + confirm flow (Priority: P1) 🔒 Constitution IV load-bearing

**Goal**: Delete a cached model from disk **only** behind an explicit confirmation naming the model + disk-freeing consequence, with a do-nothing cancel path; a single Delete activation never deletes.

**Independent Test (3 layers)**: (a) pure unit — `DeleteFromCacheAsync(alias, userConfirmed:false)` throws/no-ops and removes nothing, dylib-free; (b) DevFlow DOM — `card-delete` opens a `confirm-dialog` naming the model; `confirm-cancel` deletes nothing; no one-click destructive path; (c) live destructive check **only** on a model the test itself downloaded in US1 — never pre-existing user cache (FR-036).

- [ ] T015 [P] [US3] Create dylib-free `tests/FoundryForge.Tests/DeleteConsentGateTests.cs` proving the consent gate: `DeleteFromCacheAsync(alias, userConfirmed:false, ct)` throws (`InvalidOperationException`) / removes nothing, reached **before** any FL/dylib resolution; `userConfirmed:true` proceeds through the `IModelStateGate` (Drain). Uses the guard clause / a fake catalog substitute — touches **no** real cache (SC-006, FR-036). Write it to FAIL first if needed, then confirm green.
- [ ] T016 [US3] In `src/FoundryForge.App/Components/Catalog/ModelCard.razor`, add `data-testid="card-delete"` that **opens** the reusable `ConfirmDialog` naming the exact model and stating "confirming frees disk space" — it MUST NOT delete on this single activation (FR-010/011); there is no one-click destructive path anywhere (depends on T006).
- [ ] T017 [US3] Wire the confirm outcome: `confirm-accept` → `DeleteFromCacheAsync(alias, userConfirmed:true, ct)` through the gate, then refresh `ListCachedAsync` so the card moves to Available; `confirm-cancel` → no-op, model still cached, prior state restored (FR-012/013). Serialize delete-while-loaded through the gate so it never tears an in-flight op (FR-015); if the deleted model equals `AppSettings.DefaultModel`, leave `DefaultModel` honest (cleared/flagged), not dangling (depends on T006, T007).

**Checkpoint**: Delete is non-destructive-by-default and consent-gated; gate proven dylib-free (SC-005/006).

---

## Phase 6: User Story 4 - Cached vs available grouping (Priority: P2) — resolves KI-009

**Goal**: Group cached/installed models distinctly from available-to-download models using the authoritative cached source; resolve KI-009 (no `CachedOnly` double-filter dropping genuinely-cached models).

**Independent Test**: pure unit — `CatalogGrouping.Partition` places every model in exactly one correct group keyed off the trusted cached set; DevFlow DOM — `group-cached`/`group-available` exist, each card in exactly one, the cached group matches `ListCachedAsync` and is not empty when cached models exist.

- [ ] T018 [P] [US4] Create dylib-free `tests/FoundryForge.Tests/CatalogGroupingTests.cs`: mixed list with 2 cached aliases → correct split; a model whose `IsCached` flag disagrees with the cached set is grouped by the **set** (KI-009 proof); empty `cachedAliases` → all Available; exactly-one-group, duplicate-free, order-preserving (SC-007). Write to FAIL first.
- [ ] T019 [P] [US4] Create the pure seam `src/FoundryForge.Core/Catalog/CatalogGrouping.cs` — `static (IReadOnlyList<ModelInfo> Cached, IReadOnlyList<ModelInfo> Available) Partition(IEnumerable<ModelInfo> all, ISet<string> cachedAliases)`: Cached iff `Alias ∈ cachedAliases`; exactly one group; preserves input order; null/empty inputs ⇒ empty groups (never throws). FL-free, dylib-free (makes T018 pass).
- [ ] T020 [US4] Resolve KI-009 in `src/FoundryForge.Foundry/FoundryCatalogService.cs`: ensure `ListCachedAsync` returns the authoritative cached set from `GetCachedModelsAsync` **without** re-applying a `CachedOnly`/`Info.Cached` filter that could drop genuinely-cached models (FR-017). Record observed FL `Info.Cached` semantics on hardware for the close note (research U3).
- [ ] T021 [US4] In `src/FoundryForge.App/Components/Pages/Home.razor`, render the grouped catalog from `CatalogGrouping.Partition(all, cachedAliases)` (fed by `BrowseAsync` + `ListCachedAsync`): `data-testid="group-cached"` ("Installed / cached"), `data-testid="group-available"` ("Available to download"), `data-testid="group-label"` (text, not color-only), `data-testid="group-count"` (`aria-live="polite"`); a model moves group on refresh when its cached state changes (FR-016/018) (depends on T007, T019).

**Checkpoint**: Catalog grouped from the trusted source; KI-009 resolved (SC-007).

---

## Phase 7: User Story 5 - Variant (quantization/device) selection (Priority: P2)

**Goal**: Pin a specific variant honored by download/load; show honest "no variants reported" when none.

**Independent Test**: pure unit — `VariantSelectionState` default/pin/pin-unknown-ignored/no-variants; DevFlow DOM — `card-variant-select` pins a variant passed to download/load; a no-variant model shows honest "no variants reported" with zero fabricated options.

- [ ] T022 [P] [US5] Create dylib-free `tests/FoundryForge.Tests/VariantSelectionStateTests.cs`: multi-variant default selection; `Pin` honored; `Pin(unknown)` ignored (no fabrication); empty variants → `HasVariants=false`, `EffectiveVariantId=null` (SC-008). Write to FAIL first.
- [ ] T023 [P] [US5] Create the pure seam `src/FoundryForge.Core/Catalog/VariantSelectionState.cs` — `ctor(IReadOnlyList<ModelVariant>)`, `string? PinnedVariantId`, `string? EffectiveVariantId` (`PinnedVariantId ?? default`; null when none), `bool HasVariants`, `void Pin(string variantId)` (no-op for unknown ids). FL-free, dylib-free (makes T022 pass).
- [ ] T024 [US5] Honor the pin in `src/FoundryForge.Foundry/FoundryCatalogService.cs`: when `variantId != null`, resolve the matching `IModel` from `model.Variants` (by `Info.Id`) and call `IModel.SelectVariant(variant)` **before** download/load (FR-020); an unknown `variantId` ⇒ honest failure surfaced to the UI (no silent fallback to default) (depends on T004).
- [ ] T025 [US5] In `src/FoundryForge.App/Components/Catalog/ModelCard.razor`, add `data-testid="card-variant-select"` (+ `<label>`) bound to `VariantSelectionState.Pin(...)`, passing `EffectiveVariantId` to download/load (T009/T012); render honest "no variants reported" when `HasVariants == false` — never a fabricated option (FR-019/021) (depends on T023).

**Checkpoint**: Variant pin honored by download/load (SC-008).

---

## Phase 8: User Story 6 - Disk-space check before download (Priority: P2)

**Goal**: Non-blocking warning when a model likely won't fit free disk; honest "size unknown" when size is null. Warn-not-block.

**Independent Test**: pure unit — `DiskFitHeuristic.Evaluate` returns Fits/Warn/Unknown correctly incl. null size + boundary + negative-free throws; DevFlow DOM — a too-large model shows a non-blocking `card-diskfit` Warn (download still enabled); an unknown-size model shows honest "size unknown — can't check fit".

- [ ] T026 [P] [US6] Create dylib-free `tests/FoundryForge.Tests/DiskFitHeuristicTests.cs`: (size=2, free=100)→Fits; (size=50, free=10)→Warn; (size=null, free=100)→Unknown; (free=-1)→throws; boundary (footprint==free)→Fits (SC-009). Write to FAIL first.
- [ ] T027 [P] [US6] Create `src/FoundryForge.Core/Models/DiskFitResult.cs` — `sealed record DiskFitResult(DiskFit Fit, double? MarginGb)` + `enum DiskFit { Fits, Warn, Unknown }` (mirrors `RamFitResult`).
- [ ] T028 [P] [US6] Create the pure seam `src/FoundryForge.Core/Catalog/DiskFitHeuristic.cs` — `static DiskFitResult Evaluate(double? modelSizeGb, double freeDiskGb)`: null size ⇒ `Unknown`/`MarginGb=null` (FR-024); `freeDiskGb < 0` ⇒ `ArgumentOutOfRangeException`; `estimatedFootprint = modelSizeGb * SafetyFactor` (documented in XML-doc); `footprint <= free` ⇒ `Fits` else `Warn`; `MarginGb = round(free - footprint, 2)`. Pure, no I/O (free disk read by the caller via `DriveInfo`) (makes T026 pass; depends on T027).
- [ ] T029 [US6] In `src/FoundryForge.App/Components/Catalog/ModelCard.razor`, read free disk for the cache dir via `DriveInfo` (in the App/view-state, not the seam, R3), call `DiskFitHeuristic.Evaluate(SizeGb, freeDiskGb)`, and render `data-testid="card-diskfit"`: `Fits` (no warn) / `Warn` (non-blocking, download stays enabled — FR-023) / `Unknown` ("size unknown — can't check fit") — never a fabricated verdict (depends on T028).

**Checkpoint**: Disk-fit warns-not-blocks; honest unknown (SC-009).

---

## Phase 9: User Story 7 - Configurable model cache directory (Priority: P2) 🔒 Constitution IV (no-wipe)

**Goal**: View/change `AppSettings.ModelCacheDirectory` via the consent-aware `ISettingsService` without silently moving or wiping the existing cache; reject invalid dirs.

**Independent Test**: settings-seam unit — updating `ModelCacheDirectory` persists and moves/wipes **zero** files; DevFlow DOM — `settings-cache-dir` shows current value, invalid dir → `settings-cache-dir-error` (previous retained), valid dir → warn/confirm "nothing moved or deleted" → persists.

- [ ] T030 [P] [US7] Create dylib-free `tests/FoundryForge.Tests/SettingsCacheDirTests.cs` (settings seam): `UpdateAsync(settings with { ModelCacheDirectory = newDir })` persists the new value and performs **zero** file moves/deletes of the model cache (pointer-only, no-wipe); invalid/unwritable dir path is handled without mutating cache (SC-010, FR-026/027).
- [ ] T031 [US7] Create `src/FoundryForge.App/Components/Pages/Settings.razor` (route `/settings`, `data-testid="settings-page"`): `id="settings-cache-dir"` (+ `<label>`) showing `AppSettings.ModelCacheDirectory` via `ISettingsService.GetAsync`; `id="settings-cache-dir-save"`; `data-testid="settings-cache-dir-error"` (`role="alert"`). Hold `SettingsViewState` (current / pending / validation-error / `FreeDiskGb` via `DriveInfo`) (FR-025).
- [ ] T032 [US7] Wire the no-wipe save flow in `Settings.razor`: Save opens the reusable `ConfirmDialog` stating "existing cached models under the old directory may not appear; nothing is moved or deleted" (FR-026), then `ISettingsService.UpdateAsync` persists pointer-only; an invalid/unwritable dir is rejected with an honest error and the previous valid value retained (FR-027); if a relocated model equals `DefaultModel`, keep `DefaultModel` honest (depends on T006).

**Checkpoint**: Cache-dir editable, consent-surfaced, no silent move/wipe (SC-010).

---

## Phase 10: User Story 8 - BYOM ONNX import (Priority: P3) ⚠️ OPTIONAL — does NOT gate M3

**Goal**: *(Optional stretch)* Guided import of an Olive-compiled ONNX model into the cache; ONNX-only, honest refusal of GGUF/safetensors. **M3's definition of done does NOT depend on this story (FR-030); it may be deferred without failing the milestone.**

**Independent Test**: pure unit — the ONNX-vs-not accept/reject decision; DevFlow DOM — `byom-import` accepts ONNX + `inference_model.json`, `byom-reject` honestly refuses GGUF/safetensors, `byom-docs` links Olive/ONNX docs.

- [ ] T033 [P] [US8] *(optional)* Create dylib-free `tests/FoundryForge.Tests/OnnxImportValidatorTests.cs` + pure seam `src/FoundryForge.Core/Catalog/OnnxImportValidator.cs` — accept an Olive-compiled ONNX folder with `inference_model.json`; reject GGUF/safetensors with an honest "ONNX-only" decision (no fabrication) (FR-028/029).
- [ ] T034 [US8] *(optional)* Build the BYOM import flow UI in `src/FoundryForge.App/Components/Catalog/` (e.g. `ByomImport.razor`): `data-testid="byom-import"` (ONNX + `inference_model.json` only, placed into the configured cache dir so it surfaces as cached), `data-testid="byom-reject"` (`role="alert"`, honest "ONNX-only — GGUF/safetensors not supported"), `data-testid="byom-docs"` (Olive/ONNX docs link). Consumes only Core seams + services (no FL SDK) (depends on T033).

**Checkpoint**: If implemented, BYOM is ONNX-only and honest; if deferred, M3 DoD is unaffected (FR-030).

---

## Phase 11: Polish & Milestone Close (Cross-Cutting + Constitution II Verification)

**Purpose**: Prove M3 end-to-end without touching pre-existing user cache, close KI-009, and record the `Verified:` line. DevFlow DOM is the sanctioned autonomous evidence path (KI-001); **`ui screenshot` requires the window frontmost** and is a supplement, never the gate.

- [ ] T035 Confirm the **dylib-free seam suite is green** (CI seam gate): `DiskFitHeuristicTests`, `CatalogGroupingTests`, `VariantSelectionStateTests`, `DeleteConsentGateTests`, `SettingsCacheDirTests` (+ optional `OnnxImportValidatorTests`) and all existing suites — `dotnet test tests/FoundryForge.Tests/FoundryForge.Tests.csproj` (Quickstart Layer A; SC-006/007/008/009).
- [ ] T036 **Consent-gate + confirm-flow proof** (data preservation, Constitution IV): via DevFlow DOM on Apple Silicon, assert `card-delete` opens `confirm-dialog` naming the model with **zero** deletions on the single activation; `confirm-cancel` deletes nothing; there is **no** one-click destructive path. Combined with T015, this proves the gate dylib-free + the confirm-flow DOM (SC-005).
- [ ] T037 **DevFlow DOM e2e** (Quickstart Layer B, steps 1–9) on real Apple Silicon: grouping(KI-009) → disk-fit → variant-select → download(real progress)/cancel → auto-load → load/unload + loaded-indicator → **delete confirm-flow** → cache-dir no-wipe → negative invariants. **The single live delete targets ONLY the model the test itself downloaded — pre-existing user cache is NEVER touched (FR-036).** Record observed FL cancel-honoring + `Info.Cached` + loaded-limit behavior (research U1/U3/U4).
- [ ] T038 [P] **Negative-invariants DOM sweep** (SC-011): assert **zero** inference-parameter controls (`param-temperature`/`param-top-p`/`param-max-tokens` absent), **zero** GGUF import paths, **zero** fabricated progress bars (a `card-download-progress` exists only while a real download runs), and every genuinely-unavailable value (size/disk/variants) renders honest "unknown".
- [ ] T039 [P] **Accessibility AA verification** (FR-033): via DevFlow computed-style/DOM, confirm every new control (download/cancel/auto-load, load/unload, delete/confirm/cancel, variant selector, cache-dir editor) is labeled, keyboard-reachable, announces state; progress uses `role="progressbar"`+`aria-valuenow` (or `aria-busy` indeterminate); dialog is `role="dialog" aria-modal="true"`, focus-trapped, Esc=cancel; state conveyed text+icon (never color-alone); contrast meets AA.
- [ ] T040 Update `KNOWN-ISSUES.md`: close/annotate **KI-009** (cached-source trust) with the observed FL `Info.Cached` semantics; record observed FL download-cancel honoring (annotate if not honored) and the FL loaded-model limit; note KI-001 DOM-evidence path used.
- [ ] T041 **Independent review** (Constitution II/III, FR-035): a reviewer who is **not** the author runs `/review` over the M3 change set (UI + seams + Foundry delta), with explicit attention to the data-preservation flow (delete consent gate, confirm-flow, cache-dir no-wipe). Author does not self-approve.
- [ ] T042 Write the M3 milestone-close note with a **`Verified:`** line naming the checks that ran (unit suites + the DevFlow DOM e2e: download(progress real)/cancel/auto-load/load/unload/loaded-indicator/group/variant/disk-warn/delete-confirm-on-test-model; user cache untouched; FL cancel/`Info.Cached`/loaded-limit observations; KI-009 resolved) and record the GO/NO-GO milestone decision (depends on T035–T041).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: after Setup — **BLOCKS all stories**. T003→T004; T007 depends on T005.
- **User Stories (Phases 3–10)**: all depend on Foundational. P1 (US1/US2/US3) first for MVP; P2 (US4/US5/US6/US7) next; US8 is optional and may be deferred.
- **Polish (Phase 11)**: depends on the desired stories being complete (T037 needs US1–US7).

### User-story dependencies

- **US1, US2, US3 (P1)**: independent of each other after Foundational. US3 reuses `ConfirmDialog` (T006); US1/US2 share `ModelCard.razor` + `CatalogViewState.cs` (coordinate edits).
- **US4 (P2)**: independent; T021 fills the grouped slot scaffolded in T007. KI-009 fix (T020) is Foundry-layer.
- **US5 (P2)**: independent; T024 honors the pin threaded in T004.
- **US6 (P2)**: fully independent (new files + one `ModelCard` block).
- **US7 (P2)**: independent; reuses `ConfirmDialog` (T006); new `Settings.razor`.
- **US8 (P3)**: optional; fully isolated; deferring it does not affect DoD.

### Within each story

- Pure-seam tests (T015/T018/T022/T026/T030/T033) are written to FAIL before their seam implementation.
- Core seams before the UI that consumes them; Foundry-layer wiring before the UI calls that depend on it.
- Story complete and DevFlow-verifiable before moving to the next priority.

### Shared-file coordination (NOT parallel within a story)

- `src/FoundryForge.App/Components/Catalog/ModelCard.razor` is edited by T009/T010 (US1), T012/T014 (US2), T016/T017 (US3), T025 (US5), T029 (US6) — serialize these edits per story.
- `src/FoundryForge.Foundry/FoundryCatalogService.cs` is edited by T004 (Foundational), T020 (US4), T024 (US5) — serialize.
- `src/FoundryForge.App/Components/Pages/Home.razor` is edited by T011 (US1), T013 (US2), T021 (US4) — serialize.

### Parallel opportunities

- Setup: T002 ∥ T001.
- Foundational: T005 ∥ T006 (different files); then T007.
- Across stories (after Foundational): the pure-seam test+impl pairs are `[P]` across stories — T018/T019 (US4), T022/T023 (US5), T026/T027/T028 (US6), T030 (US7), T033 (US8) touch distinct new files and can be authored together.
- Polish: T038 ∥ T039 (different evidence sweeps).

---

## Parallel Example: pure-seam tests + impls (after Foundational)

```bash
# Write the failing dylib-free seam tests together (different files):
Task: "CatalogGroupingTests in tests/FoundryForge.Tests/CatalogGroupingTests.cs"
Task: "VariantSelectionStateTests in tests/FoundryForge.Tests/VariantSelectionStateTests.cs"
Task: "DiskFitHeuristicTests in tests/FoundryForge.Tests/DiskFitHeuristicTests.cs"
Task: "DeleteConsentGateTests in tests/FoundryForge.Tests/DeleteConsentGateTests.cs"

# Then implement the pure seams in parallel (different new files):
Task: "CatalogGrouping.cs in src/FoundryForge.Core/Catalog/"
Task: "VariantSelectionState.cs in src/FoundryForge.Core/Catalog/"
Task: "DiskFitHeuristic.cs + DiskFitResult.cs in src/FoundryForge.Core/"
```

---

## Implementation Strategy

### MVP first (US1 + US2 + US3 = the P1 manager)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL — blocks all stories) → 3. US1 (download/progress/cancel/auto-load) → 4. US2 (load/unload/indicator) → 5. US3 (consent-gated delete) → **STOP & VALIDATE** via DevFlow DOM. This is the smallest shippable, data-preservation-correct manager.

### Incremental delivery

US1 → US2 → US3 → US4 (grouping/KI-009) → US5 (variant) → US6 (disk-fit) → US7 (cache-dir) → US8 (optional BYOM) → Polish/close. Each story is independently DevFlow-verifiable and adds value without breaking the prior ones.

---

## Suggested Squad Ownership (consistent with prior milestones)

| Owner | Scope in M3 | Primary tasks |
|-------|-------------|---------------|
| **Ripley** (lead) | Curated/cache-dir decisions; milestone-close arbitration; `Verified:` sign-off + GO/NO-GO | T032 (cache-dir no-wipe decision), T042 |
| **Hicks** (Blazor UI / cards / confirm-dialog / progress) | `ModelCard` management actions, `DownloadProgress`, `ConfirmDialog`, grouped `Home`, `Settings`, AA styling | T005–T007, T008–T014, T016–T017, T021, T025, T029, T031, (T034 optional) |
| **Bishop** (FL wiring + KI-009) | FL download/cancel pass-through, variant `SelectVariant` honoring, delete wiring, **KI-009** cached-source trust | T003–T004, T020, T024 |
| **Vasquez** (packaging) | Lock-files / CI seam gate / no-new-package verification | T002 |
| **Drake** (tests / CI) | Dylib-free seam + consent-gate + settings no-wipe xUnit; negative-invariant + AA DevFlow sweeps; DevFlow DOM e2e | T001, T015, T018–T019, T022–T023, T026–T028, T030, (T033 optional), T035–T039 |
| **Spunkmeyer** (PR quality + data-preservation review) | Independent review (reviewer ≠ author); the data-preservation flow review; KNOWN-ISSUES hygiene | T040, T041 |
| **(shared)** | DevFlow e2e + frontmost-window screenshot supplement on Apple Silicon (KI-001) | T037 |

> Reviewer independence (FR-035): the author of a change set must not be its sole approver — Spunkmeyer/Ripley review what Hicks/Bishop author, and vice-versa. The data-preservation flow (T036/T037 delete confirm + cache-dir no-wipe) gets an explicit independent review pass.

---

## Notes

- `[P]` = different files, no dependency on an incomplete task.
- `[Story]` labels (US1–US8) trace each task to its spec user story; Setup/Foundational/Polish carry none.
- Pure-seam tests (T015/T018/T022/T026/T030/T033) must FAIL before their implementation lands.
- Every task preserves the M3 guardrails at the top: **data preservation** (consent gate, no-wipe, live-delete-only-on-test-model), capability honesty (real progress, honest unknowns, ONNX-only, no inference params), layering (App → services/seams only, never FL SDK), gate-serialized mutations, dylib-free seams, WCAG AA.
- Commit after each task or logical group; stop at any checkpoint to validate the story independently via DevFlow DOM (KI-001 — DOM is the autonomous evidence path; screenshots need the window frontmost).
