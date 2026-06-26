# Feature Specification: M3 — Model Install & Management

**Feature Branch**: `004-m3-model-install-management`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "M3 turns the M2 browse-only catalog into an actionable model manager: from a model card the user can download a model (with live progress + cancel + optional auto-load), load/unload it (with a clear currently-loaded indicator and loaded-state reflected on cards), delete a cached model from disk (with an explicit confirmation step — protected user data), select/pin a specific variant (quantization/device), and configure the model cache directory. Cached vs available models are clearly grouped. A disk-space check warns before a download that likely won't fit. (BYOM/Olive-ONNX import is an OPTIONAL stretch within M3 — include it as a lowest-priority story flagged optional; ONNX-only, no GGUF.)"

## Overview

M2 is DONE and GO (DEC-017): the first real screen ships a browsable, searchable, filterable catalog of Foundry Local models with honest, enriched per-model cards (real `IModel.Info` + `IModel.Variants` metadata, honest "unknown" for absent fields), a curated default view, and a cached-vs-not-cached badge. M2 was **discovery only** — by design it shipped **zero** download/load/unload/delete/variant-select/chat affordances and triggered no Foundry Local mutation.

**M3 turns that browse-only catalog into an actionable model manager.** From a model card the user can now *act*: download a model with live progress and a cancel control (and an optional "auto-load after download"); load and unload a model through the existing M1 concurrency gate, with a clear currently-loaded indicator that the cards reflect; delete a cached model from disk **only** after an explicit in-UI confirmation; pick (pin) a specific variant (quantization/device) for models that report variants; group cached vs available models distinctly; warn before a download that likely will not fit available disk; and configure the model cache directory. A lowest-priority **optional** stretch lets a user bring their own Olive-compiled ONNX model (BYOM) — ONNX-only, never GGUF.

M3 builds directly on existing seams — it adds **almost no new service surface**. `IFoundryCatalogService` already exposes `DownloadAsync(alias, IProgress<double>?, ct)`, `LoadAsync`/`UnloadAsync` (routed through `IModelStateGate`), `DeleteFromCacheAsync(alias, userConfirmed, ct)` (already consent-gated), `ListCachedAsync`/`ListLoadedAsync`, and `GetVariantsAsync`. `ISettingsService` already exposes the consent-gated `AppSettings.ModelCacheDirectory`. M3 is overwhelmingly a **UI + pure-logic-seam** milestone that wires these existing capabilities into the M2 catalog cards and a settings surface.

The single most important constraint of M3 is **data preservation (Constitution IV)**: the model cache is multi-GB protected user data. Delete requires an explicit, per-action, in-UI confirmation that names the exact model and its consequence (frees disk); there is never a one-click destructive default and there is always a cancel path that does nothing. Changing the cache directory must never silently move or wipe the existing cache. Capability honesty (Constitution IV) holds throughout: no GGUF import (ONNX-only), no fabricated progress, honest "unknown" when a size or disk figure is unavailable, no inference-parameter controls (those are M4), no fake "guaranteed" anything.

Because this is a screen-bearing milestone, success is framed around **observable UI behavior verifiable via DevFlow DOM inspection** (per KI-001 — the WebView pixel screenshot needs the window frontmost, so DOM is the sanctioned autonomous evidence path) **and the pure-logic seams** (disk-fit heuristic, cached/available grouping, variant-selection state, the consent gate) that are unit-testable without a native dylib.

## Clarifications

No outstanding clarifications. All open choices were resolved using reasonable defaults derived from `docs/PLAN.md` (the M3 milestone lines 104–110; the parity-map rows for download/manage/delete, variant selection, and BYOM/ONNX-only import; and the inference-params/structured-output honesty notes), `.specify/memory/constitution.md` (Principle IV Data Preservation & Capability Honesty; Principle V Native-Load & In-Process Discipline; Principle II Pre-Completion Verification), `.squad/decisions.md` (DEC-004 architecture/scope, DEC-016 M1-complete, DEC-017 M2-complete), `KNOWN-ISSUES.md` (KI-001 screenshot/DOM-evidence path; KI-009 CachedOnly double-filter to resolve in M3), and the real code in `src/` (`IFoundryCatalogService`, `FoundryCatalogService`, `ISettingsService`/`AppSettings`, `ModelInfo`/`ModelVariant`/`CatalogFilter`, the M2 catalog UI). The resulting defaults are recorded in the **Assumptions** section.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Download a model with live progress, cancel, and optional auto-load (Priority: P1)

As a FoundryStudio user, from a not-cached model's card I want to start a download and watch live progress (percent), cancel it if I change my mind, and optionally have the model auto-load once the download finishes — so that I can install a model and (optionally) make it immediately ready to use.

**Why this priority**: This is the headline action that turns the browse-only M2 catalog into a usable manager; "I can install a model" is the smallest slice of M3 that delivers standalone end-user value. The service capability (`DownloadAsync` with an `IProgress<double>` callback) already exists — M3 wires real progress to the card and adds the cancel and auto-load affordances.

**Independent Test**: On an Apple Silicon Mac, launch the app to "ready", pick a small not-cached model (one the test itself selects — e.g. `qwen2.5-0.5b`), trigger download from its card, and confirm via DevFlow DOM that a live percent indicator advances from a non-fabricated source (the `IProgress<double>` callback), that a Cancel control is present and aborts an in-flight download leaving the model not-cached, and that with "auto-load after download" selected the model transitions to loaded on completion. The progress-formatting and auto-load-decision logic are independently unit-testable as pure seams with no native dylib.

**Acceptance Scenarios**:

1. **Given** a not-cached model card, **When** the user starts a download, **Then** a live progress indicator shows percent sourced from the real `DownloadAsync(alias, IProgress<double>, ct)` callback (never a fabricated/animated-only bar), and the card reflects an in-progress state.
2. **Given** a download in progress, **When** the user activates Cancel, **Then** the download is cancelled via the cancellation token, the UI returns the card to a not-cached/idle state, and no partial model is presented as cached.
3. **Given** the download has completed, **When** "auto-load after download" was selected, **Then** the model is loaded (through the concurrency gate) and the card reflects loaded state; **and when** it was not selected, the model is cached but not loaded.
4. **Given** a download that fails (e.g. network loss), **When** the failure surfaces, **Then** an honest error state names the diagnosed cause and the card returns to not-cached (no silent failure, no fake completion).
5. **Given** the download controls, **When** reached via assistive technology, **Then** the progress value, the Cancel control, and the auto-load choice are labeled and the progress change is perceivable (WCAG AA).

---

### User Story 2 - Load / unload a model with a currently-loaded indicator reflected on cards (Priority: P1)

As a FoundryStudio user, from a cached model's card I want to load the model (making it ready) or unload it (freeing resources), see a clear "currently loaded" indicator, and have all cards reflect the current loaded state — with honest behavior when the system is busy and cannot load right now — so that I control which model is active.

**Why this priority**: Loading is the prerequisite for M4 chat and is a core management action; combined with US1 it completes "install and make ready." Load/unload already route through the M1 `IModelStateGate` (drain/reject) — M3 surfaces them on the cards and adds the loaded indicator and honest busy/rejected feedback.

**Independent Test**: With a cached model, trigger Load from its card and confirm via DevFlow DOM that a currently-loaded indicator appears and that the card (and any global indicator) reflect loaded state; trigger Unload and confirm the indicator clears; force a concurrent load/unload contention and confirm the gate's rejection (`ModelBusyException`) surfaces as an honest "model busy" message rather than a hang or a crash. The loaded-state reflection logic (which cards show loaded, the indicator state) is independently unit-testable as a pure seam.

**Acceptance Scenarios**:

1. **Given** a cached, not-loaded model card, **When** the user loads it, **Then** the model loads through the concurrency gate and a currently-loaded indicator is shown on that card and surfaced app-wide.
2. **Given** a loaded model, **When** the user unloads it, **Then** the model unloads through the gate and the loaded indicator clears on its card.
3. **Given** loaded state changes (load/unload completes), **When** the catalog is showing, **Then** all cards reflect the current loaded state (via refresh or polling of the real loaded list), not a stale value.
4. **Given** the number of already-loaded models is at the Foundry-Local-supported limit, **When** the user attempts another load, **Then** the limit is communicated honestly (it does not silently no-op) and the user is told what to unload first.
5. **Given** a load/unload is rejected because another mutation is in flight on that model (the gate raises `ModelBusyException`), **When** the rejection occurs, **Then** the UI surfaces an honest "model is busy, try again" state and does not hang or corrupt state.

---

### User Story 3 - Delete a cached model from disk behind an explicit confirmation (Priority: P1)

As a FoundryStudio user, from a cached model's card I want to delete the model from disk to free space, but only after an explicit confirmation step that names the exact model and tells me this frees disk space — with a cancel path that does nothing — so that I can never destroy multi-GB protected data by accident.

**Why this priority**: This is the most safety-critical action in M3 and the direct embodiment of Constitution IV (Data Preservation). It is P1 because shipping cache management without a correct, non-destructive-by-default consent flow would violate the constitution. The service method `DeleteFromCacheAsync(alias, userConfirmed, ct)` is already consent-gated (it throws without `userConfirmed`); M3 builds the in-UI confirmation that supplies that consent.

**Independent Test**: Verifiable in two non-destructive layers plus one self-contained destructive layer. (a) Pure unit test: `DeleteFromCacheAsync(alias, userConfirmed: false, ct)` throws / no-ops and removes nothing — proving the consent gate without touching any real cache. (b) DevFlow DOM: activating Delete on a cached card opens a confirmation that names the exact model and states the disk-freeing consequence; choosing Cancel closes it and the model remains cached (nothing deleted); there is no one-click destructive path. (c) Live destructive check performed **only** on a model the test itself downloaded in US1 (never on a pre-existing user-cached model): confirming the dialog removes that test model from the cached list.

**Acceptance Scenarios**:

1. **Given** a cached model card, **When** the user activates Delete, **Then** an explicit confirmation is presented that names the exact model and states that confirming frees disk space — deletion does NOT proceed on the single activation.
2. **Given** the delete confirmation, **When** the user cancels, **Then** nothing is deleted, the model remains cached, and the UI returns to its prior state.
3. **Given** the delete confirmation, **When** the user confirms, **Then** the model is removed from the cache via `DeleteFromCacheAsync(alias, userConfirmed: true, ct)` (through the concurrency gate) and the card moves to the not-cached/available state.
4. **Given** any attempt to delete without explicit confirmation, **When** the request reaches the service, **Then** it is refused (the `userConfirmed` gate throws) and nothing is removed — there is no destructive default.
5. **Given** a model that is currently loaded, **When** the user deletes it, **Then** the operation is serialized through the gate so it does not tear an in-flight operation, and the resulting state (unloaded + not-cached) is reflected honestly.

---

### User Story 4 - Cached vs available grouping in the catalog (Priority: P2)

As a FoundryStudio user, I want the catalog to clearly group/section models I already have cached separately from models that are only available to download — using the real cached state — so that I can see at a glance what is installed versus what I could install.

**Why this priority**: Grouping makes the now-actionable catalog navigable (manage what I have vs. discover what I could add) and is the natural home for resolving KI-009. It is P2 because the per-card cached badge from M2 already conveys the state; sectioning is a usability refinement layered on the P1 actions.

**Independent Test**: With a mix of cached and not-cached models, confirm via DevFlow DOM that the catalog presents a distinct "cached/installed" group and an "available" group, that each model appears in the correct group based on its real `IsCached` state, and that the cached group's membership matches the authoritative cached list (resolving KI-009 — the cached-only listing returns the right set, with no double-filter dropping genuinely-cached models). The grouping/partitioning logic is independently unit-testable as a pure seam over a list of `ModelInfo`.

**Acceptance Scenarios**:

1. **Given** the catalog with cached and not-cached models, **When** it renders, **Then** cached/installed models and available-to-download models are shown in distinct, clearly-labeled groups.
2. **Given** a model's real cached state, **When** it is placed into a group, **Then** it appears in exactly one correct group based on its authoritative `IsCached`/cached-list membership.
3. **Given** the cached-only listing path (KI-009), **When** the cached set is computed, **Then** it trusts the authoritative cached-models source and does not re-apply a `CachedOnly` filter that could drop genuinely-cached models — the cached group is neither empty-when-it-shouldn't-be nor padded with not-cached models.
4. **Given** a model's cached state changes (download completes or delete confirms), **When** the catalog refreshes, **Then** the model moves to the correct group.

---

### User Story 5 - Variant (quantization/device) selection (Priority: P2)

As a FoundryStudio user, for a model that reports multiple variants I want to pick (pin) a specific variant — by quantization and/or device — that will be used for download/load, and see an honest "no variants reported" when a model exposes none — so that I can choose the build that fits my hardware.

**Why this priority**: Variant selection is a real Foundry Local capability (`GetVariantsAsync` / `IModel.Variants`) and meaningfully affects what gets downloaded/loaded, but it refines the P1 download/load actions rather than enabling them. It is P2 because the P1 stories function with a default variant.

**Independent Test**: For a model with multiple reported variants, confirm via DevFlow DOM that the card lets the user pick a specific variant and that the selection is reflected as the one that will be used for download/load; for a model with no reported variants, confirm an honest "no variants reported" message and no fabricated options. The variant-selection state logic (which variant is selected/pinned, default selection, the no-variants case) is independently unit-testable as a pure seam.

**Acceptance Scenarios**:

1. **Given** a model that reports multiple variants (quant/device), **When** its card is shown, **Then** the variants are presented and the user can select one to pin for download/load.
2. **Given** a pinned variant, **When** the user starts a download or load, **Then** the action targets the selected variant (the selection is honored, not ignored).
3. **Given** a model that reports no variants, **When** its card is shown, **Then** an honest "no variants reported" state is displayed — never a fabricated or guessed variant.
4. **Given** the variant selector, **When** reached via assistive technology, **Then** the options and the current selection are labeled and perceivable (WCAG AA).

---

### User Story 6 - Disk-space check before download (Priority: P2)

As a FoundryStudio user, before I start a download I want a warning when the model is unlikely to fit in my available free disk space — and an honest "size unknown" when the model size is not reported — so that I am not surprised by a failed or space-exhausting download. The check warns; it does not block.

**Why this priority**: This is a "don't surprise the user" safeguard layered onto the P1 download action. It is P2 because download functions without it; the heuristic is a refinement that improves trust and avoids a poor outcome.

**Independent Test**: Drive the pure disk-fit heuristic as a unit test (no native dylib): given a model size (M2 `SizeGb`) and an available-free-disk figure, it returns "likely fits", "likely won't fit → warn", or "unknown" (when size is null) per a documented margin. In the UI, confirm via DevFlow DOM that a model whose size exceeds available free disk shows a non-blocking warning before download, that the user can still proceed, and that a model with unknown size shows an honest "size unknown — can't check fit" note rather than a false green.

**Acceptance Scenarios**:

1. **Given** a model whose reported size plus a safety margin exceeds available free disk, **When** the user is about to download, **Then** a clear non-blocking warning is shown and the user may still proceed.
2. **Given** a model that comfortably fits, **When** the user is about to download, **Then** no fit warning is shown.
3. **Given** a model whose size is unknown (null `SizeGb`), **When** the fit check runs, **Then** an honest "size unknown — fit cannot be checked" state is shown — never a fabricated fit verdict.
4. **Given** the warning, **When** it is presented, **Then** it never silently blocks the download (warn-not-block), consistent with the M2 RAM-fit honesty philosophy (a size-vs-free indicator, not a confident verdict).

---

### User Story 7 - Configurable model cache directory (Priority: P2)

As a FoundryStudio user, I want to view and change the directory where models are cached on disk — without the app silently moving or wiping my existing cache — so that I can put multi-GB models on the volume I choose.

**Why this priority**: The cache directory is a real, persisted setting (`AppSettings.ModelCacheDirectory` via the consent-aware `ISettingsService`) and a legitimate management need, but it is a settings refinement rather than a core card action. It is P2 and carries a sharp data-preservation constraint that must be encoded carefully.

**Independent Test**: Confirm via DevFlow DOM that the current cache directory is displayed and editable, and that changing it goes through the consent-aware settings update; assert (unit-testable at the settings seam) that updating `ModelCacheDirectory` persists the new value and that the operation never moves or deletes existing cached files as a side effect — any handling of the existing cache is surfaced to the user (warn/confirm), not done silently.

**Acceptance Scenarios**:

1. **Given** the settings surface, **When** it renders, **Then** the current `ModelCacheDirectory` is displayed.
2. **Given** the user enters a new cache directory, **When** they save, **Then** the new value is persisted via the consent-aware `ISettingsService` and used for subsequent downloads.
3. **Given** a cache-directory change with existing cached models, **When** the change is made, **Then** the app does NOT silently move or wipe the existing cache; the consequence (existing models may not appear under the new directory) is surfaced to the user (warn/confirm) before or at the change.
4. **Given** an invalid or unwritable directory, **When** the user tries to save it, **Then** an honest error is shown and the previous valid setting is retained.

---

### User Story 8 - [OPTIONAL stretch] Bring-your-own ONNX model (BYOM) import (Priority: P3)

As an advanced FoundryStudio user, I want a guided flow to import my own Olive-compiled ONNX model into the model cache (with a docs link) — explicitly ONNX-only — so that I can run a model I compiled myself. **This is an optional, lowest-priority stretch; it may be deferred without failing M3.**

**Why this priority**: BYOM is a genuine but advanced, narrow capability that the parity map flags as a documented limit (ONNX-only via Olive/BYOM; no GGUF/safetensors). It is P3 and explicitly optional because none of the P1/P2 management value depends on it and it must never compromise the capability-honesty guarantee (no GGUF).

**Independent Test**: If implemented, confirm via DevFlow DOM that the import flow accepts only an Olive-compiled ONNX model (with `inference_model.json`), places it into the configured cache directory so it appears as a cached model, links to docs, and explicitly refuses/omits GGUF or safetensors with an honest "ONNX-only" message. The accept/reject decision (ONNX vs not) is independently unit-testable as a pure seam.

**Acceptance Scenarios**:

1. **Given** the BYOM import flow, **When** the user selects an Olive-compiled ONNX model with its `inference_model.json`, **Then** it is placed into the configured cache directory and surfaces as a cached model in the catalog.
2. **Given** the BYOM import flow, **When** the user attempts to import a GGUF or safetensors model, **Then** it is honestly refused with an "ONNX-only — GGUF/safetensors not supported" message — never a fake or partial import.
3. **Given** the BYOM flow, **When** it is shown, **Then** it links to documentation explaining the Olive/ONNX requirement.
4. **Given** M3 is being closed, **When** BYOM is not implemented, **Then** M3 still meets its definition of done (BYOM is optional and does not gate the milestone).

---

### Edge Cases

- **Cancel races completion**: a Cancel activated as the download completes must resolve to a single coherent state (cached or not-cached), never a half-cached model presented as ready.
- **Delete while loaded**: deleting a currently-loaded model must serialize through the gate (unload/teardown then remove) so it does not tear an in-flight operation.
- **Load at capacity**: attempting to load when at the FL-supported loaded-model limit must be communicated honestly (what to unload), not silently dropped.
- **Concurrent mutations on one model**: two load/unload/delete actions on the same model must be drained/rejected by the gate (`ModelBusyException` surfaced honestly), never run concurrently.
- **Unknown size disk check**: a null `SizeGb` must yield "fit unknown", never a false "fits"/"won't fit" verdict.
- **Cache directory points at existing models elsewhere**: changing the directory must not move/wipe; models under the old directory simply may not appear — surfaced honestly.
- **Progress source missing**: if the FL `DownloadAsync` callback reports no progress, the UI must show an honest indeterminate/working state rather than a fabricated advancing percent.
- **KI-009 cached-list reliability**: if FL's `Info.Cached` is not authoritative on the cached-models path, the cached group must still be computed from the trusted cached-models source (no double-filter dropping real cached models).
- **Delete the last copy of a default/selected model**: deleting a model that is set as the default model must leave settings in an honest state (default cleared or flagged), not a dangling reference.
- **Offline during management**: load/unload/delete of already-cached models must work offline; only download requires network — surfaced honestly if offline.

## Requirements *(mandatory)*

### Functional Requirements

**Download (US1)**

- **FR-001**: Users MUST be able to start a download of a not-cached model from its card, wired to `IFoundryCatalogService.DownloadAsync(alias, IProgress<double>, ct)`.
- **FR-002**: The UI MUST display live download progress (percent) sourced from the real `IProgress<double>` callback; it MUST NOT fabricate or animate-only a progress value.
- **FR-003**: Users MUST be able to cancel an in-flight download via its cancellation token; on cancel the model MUST return to a not-cached/idle state and no partial download may be presented as cached.
- **FR-004**: Users MUST be able to choose "auto-load after download"; when selected, a successful download MUST be followed by a load (through the concurrency gate); when not selected, the model is cached but not loaded.
- **FR-005**: A failed download MUST surface an honest error naming the diagnosed cause and return the card to not-cached (no silent failure, no fake completion).

**Load / Unload (US2)**

- **FR-006**: Users MUST be able to load a cached model and unload a loaded model from its card, routed through `LoadAsync`/`UnloadAsync` (the M1 `IModelStateGate`).
- **FR-007**: The UI MUST show a currently-loaded indicator and reflect loaded state on cards (via refresh or polling of the authoritative loaded list), with no stale loaded state after a load/unload completes.
- **FR-008**: When the FL-supported loaded-model limit is reached, the system MUST communicate this honestly (state what to unload) and MUST NOT silently no-op the load.
- **FR-009**: When a load/unload is rejected by the gate (`ModelBusyException` / in-flight mutation), the UI MUST surface an honest "model busy" state and MUST NOT hang or corrupt state.

**Delete (US3) — Data Preservation (Constitution IV)**

- **FR-010**: Deletion of a cached model MUST require an explicit in-UI confirmation step; a single activation of Delete MUST NOT delete — it MUST first present the confirmation.
- **FR-011**: The confirmation MUST name the exact model and state that confirming frees disk space.
- **FR-012**: The confirmation MUST provide a Cancel path that deletes nothing and leaves the model cached.
- **FR-013**: On confirmation, deletion MUST go through `DeleteFromCacheAsync(alias, userConfirmed: true, ct)` (consent gate satisfied) and the card MUST move to the not-cached/available state.
- **FR-014**: Any delete request without explicit confirmation MUST be refused at the service (`userConfirmed: false` throws/no-ops) — there is NO destructive default and no one-click destructive path.
- **FR-015**: Deleting a currently-loaded model MUST serialize through the gate so it does not tear an in-flight operation.

**Cached vs Available grouping (US4) — resolves KI-009**

- **FR-016**: The catalog MUST present cached/installed models and available-to-download models in distinct, clearly-labeled groups, each model placed by its authoritative cached state.
- **FR-017**: The cached grouping MUST resolve KI-009: the cached set MUST be computed from the trusted cached-models source without re-applying a `CachedOnly` filter that could drop genuinely-cached models (verify FL `Info.Cached` is authoritative on that path, or trust `GetCachedModelsAsync`).
- **FR-018**: When a model's cached state changes (download completes / delete confirms), the catalog MUST move it to the correct group on refresh.

**Variant selection (US5)**

- **FR-019**: For a model that reports variants (`GetVariantsAsync` / `IModel.Variants`), users MUST be able to select (pin) a specific variant (quantization/device) to be used for download/load.
- **FR-020**: A pinned variant MUST be honored by the subsequent download/load action (the selection is not ignored).
- **FR-021**: For a model that reports no variants, the UI MUST show an honest "no variants reported" state — never a fabricated variant.

**Disk-space check (US6)**

- **FR-022**: Before a download, the system MUST compare the model's reported size (`SizeGb`) plus a documented safety margin against available free disk and warn (non-blocking) when it likely will not fit.
- **FR-023**: The disk-fit check MUST NOT block the download — the user may always proceed (warn-not-block).
- **FR-024**: When the model size is unknown (null `SizeGb`), the system MUST show an honest "size unknown — fit cannot be checked" state and MUST NOT render a fabricated fit verdict.

**Configurable cache directory (US7) — Data Preservation (Constitution IV)**

- **FR-025**: Users MUST be able to view the current `AppSettings.ModelCacheDirectory` and change it via the consent-aware `ISettingsService`.
- **FR-026**: Changing the cache directory MUST NOT silently move or wipe the existing cache; the consequence MUST be surfaced (warn/confirm) and any handling of existing files is explicit, never silent.
- **FR-027**: An invalid/unwritable directory MUST be rejected with an honest error, retaining the previous valid setting.

**BYOM ONNX import (US8 — OPTIONAL stretch)**

- **FR-028** *(optional)*: If implemented, the BYOM flow MUST accept only Olive-compiled ONNX models (with `inference_model.json`), place them into the configured cache directory so they surface as cached, and link to docs.
- **FR-029** *(optional)*: The BYOM flow MUST honestly refuse GGUF/safetensors with an "ONNX-only" message — never a fake/partial import.
- **FR-030**: M3's definition of done MUST NOT depend on BYOM (it is optional and may be deferred).

**Cross-cutting (capability honesty, architecture, accessibility, verification)**

- **FR-031**: The UI MUST consume only `IFoundryCatalogService` and `ISettingsService` (and Core pure seams); it MUST NOT call the Foundry Local SDK directly (Constitution V / DEC-004 layering).
- **FR-032**: M3 MUST NOT ship inference-parameter controls (those are M4), nor any UI for unsupported FL capabilities (no GGUF import, no fake progress, no fabricated unknowns).
- **FR-033**: All new interactive controls (download/cancel/auto-load, load/unload, delete/confirm/cancel, variant selector, cache-dir editor) MUST meet WCAG AA: labeled, keyboard-reachable, state changes perceivable, no information by color alone.
- **FR-034**: The pure-logic seams introduced by M3 (disk-fit heuristic, cached/available grouping/partition, variant-selection state, the consent gate behavior) MUST be unit-testable without a native dylib.
- **FR-035**: M3 MUST close with a real Apple-Silicon DevFlow end-to-end check (download → load → unload → consent-gated delete of a test-downloaded model, observed via DOM per KI-001) and a `Verified:` line; the reviewer MUST be independent of the author (Constitution II/III).
- **FR-036**: Verification MUST NOT wipe a user's pre-existing cached models: any live destructive delete is performed only on a model the test itself downloaded; the consent gate and the confirm-flow UI are proven without touching pre-existing cache.

### Key Entities *(include if feature involves data)*

- **ModelInfo** (existing): the catalog descriptor; M3 consumes `Alias`, `SizeGb` (for disk-fit), `Variants`, `IsCached`, `IsLoaded`, `Device`. No schema change required for the P1/P2 stories.
- **ModelVariant** (existing): `VariantId`, `Quantization`, `Device`, `SizeGb` — the unit of variant selection in US5.
- **Download operation state** (new, UI/view-model concept): per-model transient state — idle / downloading (percent) / cancelling / cached / failed — plus the auto-load choice. Not persisted; drives card rendering.
- **Loaded-state view** (existing data, new presentation): the authoritative set of loaded models (from `ListLoadedAsync`) reflected as the currently-loaded indicator and per-card loaded badges.
- **Delete confirmation** (new, UI concept): a per-action consent object naming the model and consequence; maps to the `userConfirmed` boolean on `DeleteFromCacheAsync`.
- **Variant selection state** (new, pure-logic seam): which variant is pinned for a model, its default, and the no-variants case.
- **Disk-fit result** (new, pure-logic seam): { fits | warn-won't-fit | unknown } derived from `SizeGb` + free-disk + documented margin.
- **AppSettings.ModelCacheDirectory** (existing): the persisted, consent-aware cache directory edited in US7.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From the catalog, a user can download a not-cached model and observe live percent progress that advances from the real download callback (not a fabricated bar) in 100% of download attempts on a working network.
- **SC-002**: A user can cancel an in-flight download and the model is left not-cached (no partial model shown as cached) in 100% of cancel attempts.
- **SC-003**: With "auto-load after download" selected, a completed download results in a loaded model reflected on the card in 100% of such downloads.
- **SC-004**: A user can load and unload a cached model and the currently-loaded indicator and all cards reflect the change with no stale state after the operation completes.
- **SC-005**: Deleting a cached model is impossible without an explicit confirmation that names the exact model and the disk-freeing consequence; 0 deletions occur on a single Delete activation, and choosing Cancel deletes nothing in 100% of cancellations.
- **SC-006**: A delete request without explicit confirmation is refused at the service in 100% of cases (the `userConfirmed` gate), proven by a dylib-free unit test that removes nothing.
- **SC-007**: The catalog places every model into exactly one correct group (cached vs available) based on authoritative cached state, and the cached group is never empty when cached models exist (KI-009 resolved), verified on real Apple Silicon.
- **SC-008**: For a model with multiple reported variants the user can pin one that is honored by the next download/load; for a model with none, an honest "no variants reported" is shown — with 0 fabricated variants ever displayed.
- **SC-009**: The disk-fit heuristic returns warn / fits / unknown correctly for known size vs free-disk inputs (100% of unit cases), never blocks a download, and never shows a fabricated verdict for an unknown size.
- **SC-010**: Changing the cache directory persists the new value and moves/wipes 0 existing cached files as a side effect; the consequence is surfaced to the user before/at the change.
- **SC-011**: No M3 UI ships for an unsupported capability: 0 GGUF import paths, 0 fabricated progress bars, 0 inference-parameter controls, and every genuinely-unavailable value renders as an honest "unknown".
- **SC-012**: M3 closes with a real Apple-Silicon DevFlow end-to-end run (download → load → unload → consent-gated delete of a test-downloaded model, observed via DOM) and a `Verified:` line, reviewed by an independent reviewer, with the user's pre-existing cache untouched.

## Assumptions

- **Service surface is sufficient as-is**: `IFoundryCatalogService` (`DownloadAsync` with `IProgress<double>`, `LoadAsync`/`UnloadAsync`, `DeleteFromCacheAsync(alias, userConfirmed, ct)`, `ListCachedAsync`/`ListLoadedAsync`, `GetVariantsAsync`) and `ISettingsService` (`AppSettings.ModelCacheDirectory`) already expose everything M3 needs; M3 is overwhelmingly UI + pure-logic seams, adding minimal-to-no new service abstraction.
- **DOM is the autonomous evidence path (KI-001)**: WebView pixel screenshots require the window frontmost, so DevFlow DOM inspection is the sanctioned evidence for autonomous verification; a human eyeball or frontmost-window screenshot is the supplement, not the gate.
- **KI-009 is resolved by trusting the cached source**: the cached group is computed from `GetCachedModelsAsync` without re-applying `CachedOnly`, or by verifying FL `Info.Cached` is authoritative on that path (whichever the M3 implementation confirms on hardware).
- **Loaded-model limit comes from Foundry Local**: the exact maximum number of simultaneously-loaded models is whatever FL supports at the pinned version; M3 surfaces the limit honestly rather than asserting a fixed number in the spec.
- **Disk-fit margin is a documented heuristic, not a guarantee**: consistent with the M2 RAM-fit honesty philosophy, the disk check is a size-vs-free-disk warning with a wide margin, not a confident verdict; it warns and never blocks.
- **Variant selection is informational + targeting**: pinning a variant chooses what download/load targets; it does not claim runtime performance guarantees.
- **net11.0-macos, Apple Silicon only** (DEC-004/DEC-016/DEC-017): no iOS/Android/Mac Catalyst paths; models are ONNX-only; no GGUF/safetensors import.
- **Management of cached models works offline**; only download requires network — surfaced honestly when offline.
- **BYOM (US8) is optional**: it may be deferred to a later milestone without failing M3; if implemented it is strictly ONNX/Olive with `inference_model.json` and explicitly refuses GGUF/safetensors.
- **No inference-parameter controls in M3**: temperature/max-tokens/etc. and structured-output toggles are M4 and explicitly out of scope here.
