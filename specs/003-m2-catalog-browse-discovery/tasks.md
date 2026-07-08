---
description: "Task list for M2 — Catalog Browse + Discovery"
---

# Tasks: M2 — Catalog Browse + Discovery

**Input**: Design documents from `/specs/003-m2-catalog-browse-discovery/`

**Prerequisites**: plan.md (Constitution PASS, 4-project layout), spec.md (US1–US6 + FR-001…FR-024 + SC-001…SC-012), research.md (R1–R8), data-model.md (enriched `ModelInfo`/`ModelVariant`/`ModelCapabilities`/`CatalogViewState`), contracts/ (`IFoundryCatalogService.read.md`, `core-seams.md`, `catalog-ui.dom.md`), quickstart.md (DevFlow V1–V8).

**Tests**: INCLUDED for the pure-logic Core seams only (FR-019) — `CapabilityParser`, `CuratedSelector`, `CatalogFacets`, the FL-free metadata-mapping transform, and the updated `CatalogFilterExtensions` null-device case — as xUnit in the Core-only, dylib-free `tests/FoundryForge.Tests`. UI behavior is verified as **DevFlow DOM + screenshot evidence** in the Polish/close phase (KI-001 path), not as UI unit tests.

**Organization**: Tasks are grouped by user story. P1 first (US1 browse list/cards/cached badges, US2 search), then P2 (US3 filters + cached-only, US4 curated default, US5 rich metadata, US6 honest states). Foundational holds the metadata enrichment + Core seams every UI story consumes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1…US6 for user-story phases (Setup/Foundational/Polish carry no story label)
- Exact file paths are included in every task

## Milestone guardrails (apply to every task)

- **Discovery only**: zero download/load/unload/delete/variant-select/chat affordances; no browse/search/filter/render path may trigger an FL `DownloadAsync`/`LoadAsync` (FR-013, SC-008, R7).
- **Capability honesty**: real FL metadata or an explicit "unknown / not provided" — never `0 GB`/defaulted/fabricated (FR-012, SC-007, Constitution IV).
- **Seam purity**: UI consumes only the FL-free `IFoundryCatalogService` read methods; no FL type crosses into Core/App (FR-002, Constitution V).
- **WCAG AA**: keyboard-operable, labeled, AA contrast, never color-only (FR-018, SC-010).
- **CI seam gate stays green**: pure-logic seams stay dylib-free unit-tested; if the package set changes, regenerate `packages.lock.json` (no new packages/projects expected — plan.md).

---

## Phase 1: Setup (Shared DTO extensions + component scaffold)

**Purpose**: Make the honest-nullable Core DTOs and the App view-model/component folder exist before any seam or UI work.

- [ ] T001 [P] Extend `ModelInfo` for honest enrichment in `src/FoundryForge.Core/Models/ModelInfo.cs`: change `SizeGb` → `double?`, `Device` → `Device?`, and add `ExecutionProvider`/`ContextLength`/`MaxOutputTokens`/`License`/`LicenseDescription`/`Publisher`/`ModelType`/`Capabilities` (per data-model.md §1; null/empty = unknown, never `0 GB`).
- [ ] T002 [P] Extend `ModelVariant` for honest enrichment in `src/FoundryForge.Core/Models/ModelVariant.cs`: change `Device` → `Device?` and `SizeGb` → `double?` (data-model.md §3); keep `enum Device { Cpu, Gpu, Npu }` unchanged (FL `Invalid` maps to a null `Device?`, not a new member).
- [ ] T003 [P] Create `ModelCapabilities` value type in `src/FoundryForge.Core/Models/ModelCapabilities.cs`: `readonly record struct ModelCapabilities(bool Vision, bool ToolCalling, bool Reasoning, bool ToolCallingKnown)` (data-model.md §2; tri-state-by-omission, `ToolCallingKnown=false` ⇒ "unknown").
- [ ] T004 Scaffold the App catalog component folder and view-model in `src/FoundryForge.App/Components/Catalog/CatalogViewState.cs`: `Status{Loading,Populated,Empty,Error}`, `ErrorMessage`, `AllModels`, `Filter`, `ViewMode{Curated,Full}`, `Visible`, `Facets` (data-model.md §5) — derivation left empty (wired in later stories).
- [ ] T005 Confirm no new packages/projects were introduced and the lock-file/CI seam gate stays green: verify `src/FoundryForge.Foundry/packages.lock.json` and `tests/FoundryForge.Tests/packages.lock.json` are unchanged (regenerate only if the package set changed) and the solution builds (`dotnet build FoundryForge.sln`).

**Checkpoint**: Core DTOs are honest-nullable; the `Catalog/` component folder + view-model exist; build is green.

---

## Phase 2: Foundational (Core pure seams + FL→Core enrichment — BLOCKS all UI stories)

**Purpose**: Deliver the dylib-free Core seams and the real `MapEnriched`/`GetVariantsAsync` enrichment that every UI story consumes. **⚠️ No US-phase UI work begins until this phase is complete.**

> Pure-seam tests (T006–T010) are written FIRST and MUST FAIL before their implementations (T011–T015).

### Tests for the pure-logic seams (xUnit, Core-only, dylib-free — FR-019)

- [ ] T006 [P] Write `CapabilityParserTests` in `tests/FoundryForge.Tests/CapabilityParserTests.cs` covering core-seams.md fixtures: (caps="reasoning", modalities="text,image", tool=true) → Vision+Tool+Reasoning; (null,null,null) → none + `ToolCallingKnown=false`; (modalities="text", tool=false) → none, tool-known-false.
- [ ] T007 [P] Write `CuratedSelectorTests` in `tests/FoundryForge.Tests/CuratedSelectorTests.cs`: mixed set → only curated aliases in allow-list order; a curated alias absent from input is skipped (no throw); empty input → empty; idempotent ordering.
- [ ] T008 [P] Write `CatalogFacetsTests` in `tests/FoundryForge.Tests/CatalogFacetsTests.cs`: mixed catalog with empty Task/Provider and a null-`Device` model → option lists exclude the unknowns; case-insensitive de-dup; deterministic sort; `Devices` = distinct non-null only.
- [ ] T009 [P] Write `ModelInfoMappingTests` in `tests/FoundryForge.Tests/ModelInfoMappingTests.cs`: FL-metadata fixtures → Core values via the pure transform — `FileSizeMb` null/0 → `SizeGb=null` (never `0 GB`); `DeviceType.Invalid` → `Device=null`; empty Task/Provider preserved as unknown; MB→GB conversion correct (FR-019, R1).
- [ ] T010 [P] Extend `CatalogFilterTests` in `tests/FoundryForge.Tests/CatalogFilterTests.cs` with the null-`Device` case: a model whose `Device` is null matches no device filter, still matches when no device filter is set (core-seams.md, data-model.md §4).

### Implementation of the pure-logic seams

- [ ] T011 [P] Implement `CapabilityParser.Parse(string? capabilities, string? inputModalities, bool? supportsToolCalling)` in `src/FoundryForge.Core/Catalog/CapabilityParser.cs` (R2): Vision iff modalities contain an image/vision token; ToolCalling iff `supportsToolCalling==true` with `ToolCallingKnown=HasValue`; Reasoning iff `capabilities` declares a reasoning token — never inferred from alias/name. Make T006 pass.
- [ ] T012 [P] Implement `CuratedSelector.Select(...)` + `CuratedAliases` allow-list in `src/FoundryForge.Core/Catalog/CuratedSelector.cs` (R3, FR-008): deterministic allow-list order, missing alias skipped, no quality claim. Make T007 pass. (Allow-list membership is a Ripley/lead decision — see Squad ownership.)
- [ ] T013 [P] Implement `CatalogFacets.Derive(...)` returning distinct non-empty `Tasks`/`Providers` and distinct non-null `Devices` in `src/FoundryForge.Core/Catalog/CatalogFacets.cs` (R4, FR-006): unknowns excluded, case-insensitive de-dup, deterministic order. Make T008 pass.
- [ ] T014 [P] Implement the FL-free metadata transform helper in `src/FoundryForge.Core/Catalog/MetadataMapping.cs`: pure functions for `FileSizeMb`→`SizeGb?`, FL device-int→`Device?` (Invalid→null), and unknown-string normalization, composing `CapabilityParser`. Make T009 pass (the FL-touching reader stays in Foundry — data-model.md §6).
- [ ] T015 Add null-`Device` safety to `CatalogFilterExtensions.Matches`/`Apply` in `src/FoundryForge.Core/Catalog/CatalogFilterExtensions.cs` (behavior otherwise preserved; FR-005/FR-007; no parallel predicate). Make T010 pass.

### FL→Core enrichment (Foundry project — resolves the `TODO(M2)` markers)

- [ ] T016 Replace `MapBasic` with `MapEnriched` in `src/FoundryForge.Foundry/FoundryCatalogService.cs` (resolve `TODO(M2)` ~line 139–140): map FL `ModelInfo`/`Runtime`/`IModel` → Core `ModelInfo` via the Core `MetadataMapping`/`CapabilityParser` helpers — `FileSizeMb/1024`→`SizeGb?`, `Runtime.DeviceType`→`Device?`, `ExecutionProvider`, `Task`, `ProviderType`/`Publisher`, `ContextLength`, `MaxOutputTokens`, `License`/`LicenseDescription`, `ModelType`, capabilities; honest nulls where FL omits; accurate `IsCached`/`IsLoaded`; never mutates FL state (R1, FR-011, contracts/IFoundryCatalogService.read.md).
- [ ] T017 Implement real `GetVariantsAsync` mapping in `src/FoundryForge.Foundry/FoundryCatalogService.cs` (resolve `TODO(M2)` ~line 48): map `IModel.Variants[i].Info` (`FileSizeMb`, `Runtime.DeviceType`, `Id`, parsed quantization) → `ModelVariant[]`; empty list = FL reports none; no download triggered (R1, R7). Also update `GetModelAsync`/`ListCachedAsync`/`ListLoadedAsync` to return enriched results via `MapEnriched`.

**Checkpoint**: All pure-seam tests green and dylib-free; `BrowseAsync`/`GetVariantsAsync`/`GetModelAsync`/`ListCached`/`ListLoaded` return enriched honest `ModelInfo`. UI stories can now begin.

---

## Phase 3: User Story 1 — Browse the catalog list with per-model cards + cached badges (Priority: P1) 🎯 MVP

**Goal**: After the M1 ready-gate reaches "ready", render one per-model card per available FL model through the seam, each showing alias/display name and an explicit cached-vs-not-cached badge — with zero mutation affordances.

**Independent Test**: Launch on Apple Silicon → wait for ready → DevFlow DOM shows one `[data-testid="model-card"]` per model, each with `card-alias` and a semantic `card-cached-badge` matching real cached state (cross-checked vs cached list); no download/load/chat affordance on any card.

- [ ] T018 [US1] Create the catalog page at route `/` in `src/FoundryForge.App/Components/Pages/Catalog.razor` with `data-testid="catalog-page"`, behind the M1 `AppReadyBoundary` "initializing"→"ready" gate (FR-001, catalog-ui.dom.md).
- [ ] T019 [US1] Create the list orchestrator `src/FoundryForge.App/Components/Catalog/CatalogList.razor` rendering one `ModelCard` per `CatalogViewState.Visible` model (FR-003); state regions stubbed (filled in US6).
- [ ] T020 [US1] Create the minimal per-model card `src/FoundryForge.App/Components/Catalog/ModelCard.razor` with `data-testid="model-card"` + `data-alias`, `card-alias`, `card-displayname`, and a semantic `card-cached-badge` (text "Cached"/"Not cached" + icon, never color-only) — and **no** download/load/delete/variant-select/chat affordance (FR-003/FR-004/FR-013, catalog-ui.dom.md).
- [ ] T021 [US1] Wire `CatalogViewState` load in `src/FoundryForge.App/Components/Catalog/CatalogViewState.cs` + `Catalog.razor`: call `IFoundryCatalogService.BrowseAsync` and cross-reference `ListCachedAsync` for accurate `IsCached`; read-only seam usage only (FR-002, R7).
- [ ] T022 [US1] Demote the M1 chat-smoke landing: make `Catalog` the `/` route and remove/redirect `src/FoundryForge.App/Components/Pages/Home.razor`, updating `src/FoundryForge.App/Components/Routes.razor` as needed (plan.md structure).
- [ ] T023 [US1] Add catalog/card/cached-badge styles to `src/FoundryForge.App/Components/wwwroot/app.css` meeting AA contrast with text+icon badges (no color-only — FR-004/FR-018).
- [ ] T024 [US1] Apply WCAG AA to the list/cards: keyboard-reachable cards, labeled badges, logical focus order, and the stable `id`/`data-testid` hooks from catalog-ui.dom.md (FR-018, SC-010).

**Checkpoint**: A populated, accessible catalog renders one honest card per model with an accurate cached badge and zero mutation affordances — the MVP screen.

---

## Phase 4: User Story 2 — Search by alias, display name, or id (Priority: P1)

**Goal**: A search box narrows visible cards to models whose alias/display name/id contains the query case-insensitively, wired to `CatalogFilter.SearchText` (reuse the M1 seam, no new predicate).

**Independent Test**: DevFlow DOM sets `#catalog-search` value + dispatches input → visible cards narrow to exactly the case-insensitive matches; clearing restores the full list; the same narrowing is reproduced by the existing `CatalogFilter`/`CatalogFilterExtensions` unit tests with no dylib.

- [ ] T025 [US2] Create the filters component `src/FoundryForge.App/Components/Catalog/CatalogFilters.razor` with the search input `id="catalog-search"` + associated `<label>` (FR-005, catalog-ui.dom.md).
- [ ] T026 [US2] Wire `SearchText` into the `CatalogViewState` derivation (`Filter.Apply(AllModels)` over the full catalog) in `src/FoundryForge.App/Components/Catalog/CatalogViewState.cs` — no parallel filtering logic (FR-005/FR-007/FR-009).
- [ ] T027 [US2] Add the result-count live region `data-testid="catalog-count"` `aria-live="polite"` in `src/FoundryForge.App/Components/Catalog/CatalogList.razor` so narrowing is announced (US2 AC-4, SC-010).
- [ ] T028 [US2] Render a basic "no models match" placeholder when search yields zero results in `CatalogList.razor` (distinct from a blank screen; full empty/loading/error states hardened in US6 — FR-015).

**Checkpoint**: Search narrows the full catalog case-insensitively with an announced count; clearing restores the list.

---

## Phase 5: User Story 3 — Filter by device, task, provider + cached-only (Priority: P2)

**Goal**: Device/Task/Provider filters and a cached-only toggle, each wired to the matching `CatalogFilter` field, intersecting (AND) with search, with honest "unknown" facet handling and a reset affordance.

**Independent Test**: DevFlow DOM selects a device/task/provider and toggles cached-only → visible cards reduce to exactly the matching set; combined search+filters intersect identically to `CatalogFilterExtensions.Matches`; null/unknown-facet models are not mislabeled; reset restores the default.

- [ ] T029 [US3] Add device (`id="filter-device"`), task (`id="filter-task"`), provider (`id="filter-provider"`) controls and the cached-only toggle (`id="filter-cached"`) to `src/FoundryForge.App/Components/Catalog/CatalogFilters.razor` (FR-006, catalog-ui.dom.md).
- [ ] T030 [US3] Populate facet option lists from `CatalogFacets.Derive(AllModels)` in `src/FoundryForge.App/Components/Catalog/CatalogViewState.cs` (excludes unknown Task/Provider and null Device — US3 AC-6, R4).
- [ ] T031 [US3] Wire `Device`/`Task`/`Provider`/`CachedOnly` into the `CatalogFilter` and the `Filter.Apply` intersection in `CatalogViewState.cs`, combining with `SearchText` as a logical AND (FR-006/FR-007).
- [ ] T032 [US3] Add a discoverable reset/clear affordance `id="catalog-reset"` in `CatalogFilters.razor` that restores the curated default view (FR-007).
- [ ] T033 [US3] Apply WCAG AA to all filter controls in `src/FoundryForge.App/Components/Catalog/CatalogFilters.razor`: associated labels, operable by keyboard, selected state announced (FR-018, US3 AC-5).

**Checkpoint**: Filters + cached-only intersect with search honestly; unknown facets are not bucketed; reset works.

---

## Phase 6: User Story 4 — Curated default view surfaced first (Priority: P2)

**Goal**: On a fresh launch with no search/filter, show a deterministic curated subset labeled as such, with a single discoverable action to reveal the full catalog; search/filter always run over the full catalog.

**Independent Test**: Launch with no query/filter → DevFlow DOM shows a `curated-banner`-labeled subset (not "all models"); `#view-toggle` reveals the full catalog; searching for a non-curated model still finds it (search runs over the full set).

- [ ] T034 [US4] Add the curated/full `id="view-toggle"` control to `src/FoundryForge.App/Components/Catalog/CatalogFilters.razor` bound to `CatalogViewState.ViewMode` (FR-008).
- [ ] T035 [US4] Add the curated label `data-testid="curated-banner"` ("Recommended / curated by FoundryForge", no quality claim) in `src/FoundryForge.App/Components/Catalog/CatalogList.razor` (FR-008, R3).
- [ ] T036 [US4] Implement the derivation `Visible = (ViewMode==Curated && Filter.IsEmpty) ? CuratedSelector.Select(AllModels) : Filter.Apply(AllModels)` in `CatalogViewState.cs` — any active search/filter forces evaluation over the full catalog (FR-009, data-model.md §5).

**Checkpoint**: Curated default shows first and is labeled; full catalog is one action away; search/filter never confined to the curated subset.

---

## Phase 7: User Story 5 — Rich, honest per-model card metadata (Priority: P2)

**Goal**: Render the enriched fields on each card — size (GB), device/EP, context length, capabilities, license, variant availability — with explicit "unknown / not provided" wherever FL omits a value (never `0 GB`).

**Independent Test**: For a known model, DevFlow DOM shows real FL values in `card-size`/`card-device`/`card-context`/`card-license`/`card-variants`/`card-capabilities` (not M1 stub defaults); at least one genuinely-absent field renders "unknown / not provided"; the mapping is covered by the dylib-free `ModelInfoMappingTests` (T009).

- [ ] T037 [US5] Render the enriched fields on `src/FoundryForge.App/Components/Catalog/ModelCard.razor`: `card-size`, `card-device` (device + execution provider), `card-context`, `card-license` with the catalog-ui.dom.md `data-testid` hooks (FR-010).
- [ ] T038 [US5] Render capability chips `data-testid="card-capabilities"` from `ModelCapabilities` (vision/tool/reasoning shown only where FL declares; tool may render "unknown" when `ToolCallingKnown=false`) in `ModelCard.razor` (FR-010, R2).
- [ ] T039 [US5] Render honest "unknown / not provided" for every null/empty enriched field (size/device/EP/context/license) in `ModelCard.razor` — never `0 GB`/defaulted/fabricated (FR-012, SC-007).
- [ ] T040 [US5] Render variant availability `data-testid="card-variants"` (count/list or "no variants reported") in `ModelCard.razor` — informational only, no variant-select-to-load affordance (FR-010, US5 AC-3).
- [ ] T041 [US5] Apply WCAG AA field associations/labels for card metadata and add card-detail styles to `src/FoundryForge.App/Components/wwwroot/app.css` so information is not conveyed by layout/color alone (FR-018, US5 AC-5).

**Checkpoint**: Every card shows real FL metadata or an explicit honest "unknown"; variants are informational; no fabricated values.

---

## Phase 8: User Story 6 — Honest loading, empty, and error states (Priority: P2)

**Goal**: Mutually-exclusive, distinct, accessible loading / empty(no-match) / error states; the error state names the diagnosed cause with a retry; a fetch failure is never shown as a silent empty list.

**Independent Test**: Drive each state via DevFlow DOM — loading during init/fetch (`catalog-loading`), distinct `catalog-empty` for an over-constrained filter with reset, and an actionable `catalog-error` + `#catalog-retry` on a simulated/real fetch failure — each announced to assistive tech.

- [ ] T042 [US6] Implement the mutually-exclusive `Status{Loading,Populated,Empty,Error}` rendering in `src/FoundryForge.App/Components/Catalog/CatalogList.razor`: `data-testid="catalog-loading"` (FR-014), `data-testid="catalog-empty"` (FR-015), `data-testid="catalog-error"` (FR-016) — visually and semantically distinct.
- [ ] T043 [US6] Map a thrown catalog/FL fetch exception to `Status=Error` with `ErrorMessage`=diagnosed cause in `CatalogViewState.cs`, and add the retry button `id="catalog-retry"` that re-invokes `BrowseAsync` (FR-016, contracts/IFoundryCatalogService.read.md error contract — never a silent empty).
- [ ] T044 [US6] Resolve `Status=Empty` when `Visible` is empty but `AllModels` non-empty with an active filter, wiring the reset affordance from T032; ensure Empty ≠ Loading ≠ Error (FR-015, data-model.md §5).
- [ ] T045 [US6] Add the manual refresh affordance `id="catalog-refresh"` in `CatalogFilters.razor`/`Catalog.razor` that re-invokes `BrowseAsync`/`ListCachedAsync` only — no download/load/mutation (FR-017, R7).
- [ ] T046 [US6] Apply WCAG AA to the three states: roles/labels and an `aria-live` announcement so loading/empty/error and the retry/reset actions are announced and operable (FR-018, US6 AC-4).

**Checkpoint**: All three honest states are observable, distinct, accessible; fetch failures surface the cause with retry, never a fake-empty.

---

## Phase 9: Polish, Cross-Cutting & Milestone Close

**Purpose**: KI-008 hardening, final AA pass, the non-mutation/negative-invariant proof, the real Apple-Silicon DevFlow e2e + screenshot re-verification, independent review, and the closing `Verified:` note.

- [ ] T047 [P] KI-008 dispose hardening in `src/FoundryForge.Foundry/FoundryLifecycle.cs`: volatile `_disposed`, dispose under the init lock, continuation-dispose for a late-completing init — or record an honest deferral in the closing note if it cannot be safely completed (FR-022, R8, SC-012).
- [ ] T048 [P] Finalize AA contrast and no-color-only styling across catalog/card/badge/filter/state CSS in `src/FoundryForge.App/Components/wwwroot/app.css` (verifiable via DevFlow computed-style query — FR-018, SC-010).
- [ ] T049 Verify the negative DOM invariants are absent on every card via DevFlow `Runtime evaluate`: zero `[data-testid="card-download"|"card-load"|"card-delete"|"card-variant-select"|"card-chat"]` (FR-013, SC-008, catalog-ui.dom.md).
- [ ] T050 Run the real Apple-Silicon DevFlow end-to-end check per quickstart.md V1–V8: launch → ready-gate → catalog renders one card per model → search and each filter change the visible list → curated/full toggle → loading/empty/error states; capture DOM evidence (KI-001 primary path) and screenshots as evidence (FR-023, SC-001/SC-011).
- [ ] T051 Re-verify the `maui devflow ui screenshot` path against the live catalog (R6): if it captures the WKWebView, attach screenshots and update `KNOWN-ISSUES.md` KI-001 to "upstream-fixed-pending-removal"; if still blank, fall back to DOM snapshots + native window capture and record KI-001 honestly as still-blocked.
- [ ] T052 Re-verify KI-001 outcome is recorded and prove SC-008 non-mutation: confirm a non-cached model remains non-cached after a full browse/search/filter session (0 FL download/load operations triggered) and note the result.
- [ ] T053 Obtain an independent `/review` of the M2 change set: author ≠ sole approver; surface only genuine bugs/security/logic issues (FR-024, SC-011, Constitution III).
- [ ] T054 Write the M2 milestone-closing note ending with a `Verified:` line naming the checks that ran (build + dylib-free unit tests green + DevFlow V1–V8 DOM + screenshot re-verification outcome + independent approval), a decision summary, and the KI-008 completed/deferred status (FR-023, SC-011/SC-012, Constitution II).
- [ ] T055 Record any M2 workaround with a tracking link in `KNOWN-ISSUES.md` and confirm no UI/toggles exist for unsupported FL capabilities or M3+ scope (FR-020/FR-021, SC-012).

**Checkpoint**: M2 is verified end-to-end on real hardware, independently approved, and honestly documented.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup DTOs (T001–T003); **BLOCKS all user stories**.
- **User stories (Phases 3–8)**: all depend on Foundational completion.
  - US1 (P1) and US2 (P1) are the MVP and come first.
  - US3 depends on US1 (the list) **and** on the enriched device/task/provider metadata from Foundational (T016) to be meaningful.
  - US4 depends on US1 + `CuratedSelector` (T012).
  - US5 deepens US1 cards using the enrichment (T016/T017).
  - US6 hardens the states orchestrated in US1/US2's `CatalogList`.
- **Polish (Phase 9)**: depends on the desired user stories being complete.

### Within each story

- Pure-seam tests (T006–T010) precede their implementations (T011–T015) and must fail first.
- DTOs → Core seams → Foundry enrichment → page → list → card → filters → states.
- Story complete and independently testable before moving to the next priority.

### Parallel opportunities

- **Setup**: T001, T002, T003 in parallel (different files).
- **Foundational tests**: T006–T010 in parallel; then implementations T011–T014 in parallel (T015 edits the existing `CatalogFilterExtensions`; T016/T017 share `FoundryCatalogService.cs` — sequential).
- **Cross-story**: once Foundational is done, US1 and US2 can be staffed together; US3/US4/US5/US6 can be split across owners since they touch largely distinct files (`CatalogFilters.razor`, `CatalogList.razor`, `ModelCard.razor`, `CatalogViewState.cs`) — coordinate edits to shared `CatalogViewState.cs`.
- **Polish**: T047 and T048 in parallel (different files).

---

## Parallel Example: Foundational pure seams

```bash
# Write the failing pure-seam tests together (different files):
Task: "CapabilityParserTests in tests/FoundryForge.Tests/CapabilityParserTests.cs"
Task: "CuratedSelectorTests in tests/FoundryForge.Tests/CuratedSelectorTests.cs"
Task: "CatalogFacetsTests in tests/FoundryForge.Tests/CatalogFacetsTests.cs"
Task: "ModelInfoMappingTests in tests/FoundryForge.Tests/ModelInfoMappingTests.cs"

# Then implement the seams in parallel (different new files):
Task: "CapabilityParser.cs in src/FoundryForge.Core/Catalog/"
Task: "CuratedSelector.cs in src/FoundryForge.Core/Catalog/"
Task: "CatalogFacets.cs in src/FoundryForge.Core/Catalog/"
Task: "MetadataMapping.cs in src/FoundryForge.Core/Catalog/"
```

---

## Implementation Strategy

### MVP first (US1 + US2)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL — blocks all stories) → 3. US1 (browse list/cards/cached badges) → **STOP & VALIDATE** via DevFlow DOM → 4. US2 (search). This is the smallest shippable discovery screen.

### Incremental delivery

US1 → US2 → US3 (filters) → US4 (curated) → US5 (rich metadata) → US6 (honest states) → Polish/close. Each story is independently DevFlow-verifiable and adds value without breaking the prior ones.

---

## Suggested Squad Ownership (consistent with prior milestones)

| Owner | Scope in M2 | Primary tasks |
|-------|-------------|---------------|
| **Ripley** (lead) | Curated-list membership + sequencing decisions; milestone-close arbitration; `Verified:` sign-off | T012 (allow-list decision), T054 |
| **Hicks** (Blazor UI / cards / filters) | Catalog page, list, card, filters, states, AA styling | T004, T018–T046 (UI), T048 |
| **Bishop** (FL metadata enrichment) | FL→Core `MapEnriched`/`GetVariantsAsync`, Core seams + DTOs, KI-008 hardening | T001–T003, T011–T017, T047 |
| **Vasquez** (packaging) | Lock-files / CI seam gate / no-new-package verification | T005 |
| **Drake** (tests / CI) | Pure-seam xUnit + null-device case; non-mutation + negative-invariant DevFlow checks | T006–T010, T049, T052 |
| **Spunkmeyer** (PR quality) | Independent review; KNOWN-ISSUES hygiene; honest-state polish | T053, T055 |
| **(shared)** | DevFlow e2e + screenshot re-verification on Apple Silicon | T050, T051 |

> Reviewer independence (FR-024): the author of a change set must not be its sole approver — Spunkmeyer/Ripley review what Hicks/Bishop author, and vice-versa.

---

## Notes

- `[P]` = different files, no dependency on an incomplete task.
- `[Story]` labels (US1–US6) trace each task to its spec user story; Setup/Foundational/Polish carry none.
- Pure-seam tests must fail before their implementation (T006–T010 before T011–T015).
- Every UI task must preserve the discovery-only, capability-honesty, seam-purity, and WCAG AA guardrails listed at the top.
- Commit after each task or logical group; stop at any checkpoint to validate the story independently via DevFlow DOM.
