# Feature Specification: M2 — Catalog Browse + Discovery

**Feature Branch**: `003-m2-catalog-browse-discovery`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "M2 delivers the catalog browse + discovery experience — the first real screen of FoundryForge. A user opens the app (which reaches 'ready' via the M1 lifecycle gate) and sees a browsable, searchable, filterable catalog of Foundry Local models with informative per-model cards, so they can find a model to download/run. M2 is browse/discovery only: it does NOT download, load, or chat (those are M3/M4) — but cards must clearly indicate cached vs not-cached state."

## Overview

M1 is DONE and GO (DEC-016): the real `net11.0-macos` AppKit head + Blazor Hybrid skeleton, the single `FoundryLocalManager` behind the off-dispatcher `ReadyAsync` gate, the load/unload concurrency gate, the FL-free `IFoundryCatalogService` seam, the pure-logic `CatalogFilter`/`CatalogFilterExtensions` filtering seam, settings, and the dylib-free test/CI seam all exist and are proven on real Apple Silicon. M1 shipped **no end-user feature** — the Home page is a foundation smoke only.

**M2 builds the first real end-user screen on that foundation.** A user launches the app, the M1 ready-gate transitions from "initializing" to "ready", and they land on a browsable catalog of Foundry Local models: a curated default view, full-text search, device/task/provider filters, a cached-only toggle, and informative per-model cards. The single most important honesty constraint of M2 is that it is **discovery only** — no model is downloaded, loaded, deleted, or chatted with (those are M3/M4). Cards clearly distinguish cached from not-cached state, but the actions that change that state do not exist yet.

M2 also turns the M1 catalog service's currently-stubbed metadata (`MapBasic` returns `SizeGb: 0`, `Device.Gpu`, empty `Task`/`Provider`, empty `Variants` — see the `TODO(M2)` markers in `src/FoundryForge.Foundry/FoundryCatalogService.cs`) into **honest, enriched metadata** sourced from real Foundry Local model metadata, with an honest "unknown / not provided" rendering wherever a field is genuinely unavailable from FL (capability honesty, Constitution IV).

Because this is the first screen-bearing milestone, success is framed around **observable UI behavior verifiable via DevFlow** (DOM inspection + the documented screenshot caveat KI-001) **and the pure-logic filter seams** that are unit-testable without a native dylib.

## Clarifications

No outstanding clarifications. All open choices were resolved using reasonable defaults derived from `docs/PLAN.md` (the M2 milestone line, the LM Studio parity-map catalog/variant rows, and the competitive-positioning section), the constitution, `.squad/decisions.md` (DEC-004 architecture, DEC-014 positioning, DEC-016 M1-complete), `KNOWN-ISSUES.md` (KI-001 screenshot caveat, KI-007/KI-008), the completed M1 spec at `specs/002-m1-app-shell-foundation/`, and the real code in `src/`. The resulting defaults are recorded in the **Assumptions** section.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse the catalog list with per-model cards and cached badges (Priority: P1)

As a FoundryForge user, when the app reaches "ready" I want to see a browsable list of the available Foundry Local models rendered as informative per-model cards, each clearly showing whether the model is already cached on my machine — so that I can survey what is available and identify candidates to (later) download and run.

**Why this priority**: This is the MVP of M2 and the first real screen of the product. Without the catalog list and cards there is no discovery experience at all; everything else (search, filters, curated view, enrichment) refines a list that must first exist. It is the smallest slice that delivers standalone user value: "I can see what models exist and which I already have."

**Independent Test**: Launch the app on an Apple Silicon Mac, wait for the M1 ready-gate to reach "ready", and confirm via DevFlow DOM inspection that a catalog list renders one card per available Foundry Local model, that each card shows at least the model alias and a cached-vs-not-cached badge, and that the cached badge value matches the model's actual cached state (cross-checked against the cached list). No download/load/chat affordance is present on any card.

**Acceptance Scenarios**:

1. **Given** the app has reached the "ready" state, **When** the catalog screen renders, **Then** it shows one card per available Foundry Local model sourced through the `IFoundryCatalogService` seam (the UI never calls the FL SDK directly).
2. **Given** a model that is present in the local cache, **When** its card renders, **Then** the card shows an explicit "cached" badge; **and given** a model not in the cache, its card shows an explicit "not cached" (available-to-download) state — the two states are visually and semantically distinct.
3. **Given** the rendered catalog, **When** a card is inspected, **Then** it exposes no download, load, delete, variant-select, or chat affordance (M2 is discovery only; those belong to M3/M4).
4. **Given** the catalog list, **When** it is navigated via keyboard and assistive technology, **Then** every card and its state badge are reachable and announced (WCAG AA), with no information conveyed by color alone.

---

### User Story 2 - Search the catalog by alias, display name, or id (Priority: P1)

As a FoundryForge user, I want to type into a search box and have the catalog narrow to models whose alias, display name, or id matches what I typed (case-insensitive) — so that I can quickly find a specific model in a catalog of dozens without scrolling.

**Why this priority**: Search is the primary find mechanism and, like the list itself, delivers immediate standalone value on top of US1. The matching logic already exists as a tested pure-logic seam (`CatalogFilter.SearchText` + `CatalogFilterExtensions.Matches`); M2 wires the UI to it. It is P1 because a catalog without search is materially harder to use even at the M0d-observed catalog size (~46 models).

**Independent Test**: With the catalog rendered, drive a search query through the UI (DevFlow DOM: set the search input value and dispatch input) and confirm the visible cards narrow to exactly the models whose alias, display name, or id contains the query case-insensitively; confirm the same narrowing is independently verifiable as a unit test over `CatalogFilter`/`CatalogFilterExtensions` with no native dylib.

**Acceptance Scenarios**:

1. **Given** the catalog is rendered, **When** the user types a query, **Then** the visible cards narrow to models whose alias OR display name OR id contains the query, case-insensitively, wired to `CatalogFilter.SearchText` (no new filtering logic — reuse the M1 seam).
2. **Given** a search query that matches nothing, **When** filtering completes, **Then** an honest "no models match" empty state is shown (not a blank screen or a spinner), and clearing the query restores the full list.
3. **Given** leading/trailing whitespace or mixed case in the query, **When** matching runs, **Then** results are unaffected by case and surrounding whitespace (consistent with the existing seam behavior).
4. **Given** the search input, **When** reached via assistive technology, **Then** it has an accessible label and the result-count change is perceivable.

---

### User Story 3 - Filter by device, task, and provider, plus a cached-only toggle (Priority: P2)

As a FoundryForge user, I want to filter the catalog by device (CPU/GPU/NPU), by task, and by provider, and to toggle "cached only" — so that I can focus on, for example, only NPU-capable models, only the models I already have downloaded, or only a specific provider's models.

**Why this priority**: Filters are a strong refinement on top of browse + search but are not required for the MVP to deliver value. They depend on US1 (the list) and on US5's enriched device/task/provider metadata to be meaningful (an unenriched catalog has nothing to filter by). They map directly onto the existing `CatalogFilter` fields (`Device`/`Task`/`Provider`/`CachedOnly`), so the work is UI wiring plus honest handling of "unknown" facet values.

**Independent Test**: With the catalog rendered, apply each filter via the UI (DevFlow DOM) — select a device, a task, a provider, and toggle cached-only — and confirm the visible cards reduce to exactly the matching set, and that combined filters intersect (AND) consistently with the existing `CatalogFilterExtensions.Matches` predicate; confirm the same intersections are unit-testable over `CatalogFilter` without a dylib.

**Acceptance Scenarios**:

1. **Given** the catalog, **When** the user selects a device (CPU, GPU, or NPU), **Then** only models matching that device (including via a device variant, per the existing `Matches` rule) remain visible, wired to `CatalogFilter.Device`.
2. **Given** the catalog, **When** the user selects a task and/or a provider, **Then** the list narrows to models with that task/provider (case-insensitive), wired to `CatalogFilter.Task`/`CatalogFilter.Provider`.
3. **Given** the catalog, **When** the user enables the "cached only" toggle, **Then** only cached models remain visible, wired to `CatalogFilter.CachedOnly`; **and when** disabled, the full set returns.
4. **Given** multiple filters and a search query active at once, **When** filtering runs, **Then** they combine as an intersection (all must match), and an honest empty state appears if nothing matches; a clear "reset filters" affordance restores the default view.
5. **Given** filter controls (device/task/provider/cached-only), **When** used via keyboard and assistive technology, **Then** each control is labeled, operable, and its selected state is announced (WCAG AA).
6. **Given** models whose device/task/provider is genuinely unknown (not provided by FL), **When** facet options are presented, **Then** "unknown / not provided" is handled honestly (e.g., such models are not silently mislabeled into a real facet value).

---

### User Story 4 - Curated default view surfaced first (Priority: P2)

As a FoundryForge user opening the app for the first time, I want a sensible curated/recommended subset of models surfaced first (with the full catalog still one action away) — so that I am not dropped into an undifferentiated wall of dozens of models and can start from a trustworthy short list, reinforcing the "curated, trusted catalog" positioning.

**Why this priority**: The curated default improves first-run experience and reinforces DEC-014 positioning ("curated catalog as a feature, not a limitation"), but the screen is fully functional without it (US1 already shows the full list). It is a P2 refinement that depends on US1.

**Independent Test**: Launch to the catalog with no search/filter applied and confirm via DevFlow DOM that a curated/recommended subset is presented as the default view, that the view is clearly labeled as curated (not "all models"), and that a single, discoverable action reveals the full catalog; confirm the curated-selection rule is deterministic and independently inspectable.

**Acceptance Scenarios**:

1. **Given** a fresh launch with no search or filter applied, **When** the catalog renders, **Then** a curated/recommended subset is shown first and is labeled as such (distinct from the full list).
2. **Given** the curated default view, **When** the user chooses to see everything, **Then** a single discoverable action switches to the full catalog (all available models).
3. **Given** any active search or filter, **When** results are computed, **Then** they apply to the full catalog (search/filter is never silently confined to only the curated subset).
4. **Given** the curated selection, **When** it is derived, **Then** the selection rule is deterministic and honest (no fabricated "recommended" claims about model quality that FL metadata does not support).

---

### User Story 5 - Rich, honest per-model card metadata (Priority: P2)

As a FoundryForge user evaluating models, I want each card to show the model's real details — alias, display name, size (GB), device / execution provider, context length, capabilities (e.g. vision / tool / reasoning where available), license, the cached badge, and variant availability (count/list of quant/device variants) — with anything FL genuinely does not provide shown honestly as "unknown / not provided" rather than a fabricated value — so that I can make an informed choice and trust what the app tells me.

**Why this priority**: Enrichment is what makes the cards genuinely useful and is the substance behind the device/task/provider filters (US3), but the screen renders and is navigable without it (US1 cards can start minimal). It is the work of replacing the M1 `MapBasic` stub (`TODO(M2)` in `FoundryCatalogService.cs`) with real metadata mapping. It is P2 because the list/search MVP (US1/US2) delivers value first, and enrichment then deepens every card.

**Independent Test**: For a known model, inspect its card via DevFlow DOM and confirm each present field reflects the real Foundry Local metadata for that model (size, device/EP, context length, capabilities, license, variants) rather than the M1 stub defaults (`0` GB, hardcoded GPU, empty task/provider, empty variants); confirm that at least one field FL does not provide for some model renders an explicit "unknown / not provided" rather than a placeholder or fabricated value; confirm the metadata-mapping is covered by a unit test that needs no dylib (mapping pure FL-metadata fixtures to the card's display model).

**Acceptance Scenarios**:

1. **Given** a model with full FL metadata, **When** its card renders, **Then** it shows alias, display name, size in GB, device / execution provider, context length, capabilities (vision/tool/reasoning where available), license, the cached badge, and variant availability — sourced from real FL model metadata via the enriched catalog service (replacing the M1 `MapBasic` stub).
2. **Given** a field that Foundry Local genuinely does not provide for a model, **When** the card renders that field, **Then** it shows an explicit "unknown / not provided" (or omits the field with an honest indicator) and never a fabricated or default-stand-in value (Constitution IV capability honesty).
3. **Given** a model with multiple quantization/device variants, **When** its card renders, **Then** it indicates variant availability (a count or list); **and given** variant *selection-to-load* is requested, **then** no such affordance exists (variant selection to load is M3).
4. **Given** the enrichment, **When** size is shown, **Then** it is the model's real size (e.g., derived from FL file-size metadata), and **when** a model's size is unavailable, the card says so honestly rather than showing `0 GB`.
5. **Given** card metadata, **When** read via assistive technology, **Then** each field has an accessible label/association so the information is not conveyed by visual layout alone (WCAG AA).

---

### User Story 6 - Honest loading, empty, and error states (Priority: P2)

As a FoundryForge user, I want the catalog to honestly tell me when it is loading, when a filter/search matched nothing, and when Foundry Local or the catalog could not be reached — so that I am never left staring at a blank screen or a fabricated result, and I know whether to wait, change my filter, or retry.

**Why this priority**: Honest states are required for a trustworthy, accessible screen and are mandated by the constitution ("surface the diagnosed cause, not a generic failure"), but they are a hardening layer over the core browse/search/filter stories. P2 because US1/US2 deliver the happy-path value first; this story makes the unhappy paths honest.

**Independent Test**: Drive each state and confirm the correct honest UI: (a) during catalog initialization/fetch, a loading indicator is shown (and the M1 "initializing" gate still blocks the screen until ready); (b) a search/filter that matches nothing shows a distinct "no results" empty state with a reset affordance; (c) when the catalog fetch fails (FL/catalog error), a clear, actionable error state is shown with a retry, surfacing the diagnosed cause rather than a generic or fake-empty result. Each state is verifiable via DevFlow DOM.

**Acceptance Scenarios**:

1. **Given** the app is still reaching "ready" or the catalog is being fetched, **When** the screen renders, **Then** an honest loading state is shown (consistent with the M1 initializing gate) rather than a blank or a misleading empty list.
2. **Given** an active search/filter that matches no models, **When** results are computed, **Then** a distinct "no models match your filters" empty state is shown with a way to clear/reset, clearly different from the loading state and from a fetch error.
3. **Given** the catalog fetch fails (FL unavailable or catalog error), **When** the failure is surfaced, **Then** an honest error state names that the catalog could not be loaded and offers a retry — never a silent empty list presented as if zero models exist, and never a fabricated cause (Constitution: diagnosed cause, not a generic failure).
4. **Given** any of the loading / empty / error states, **When** reached via assistive technology, **Then** the state and any retry/reset action are announced and operable (WCAG AA).

---

### Edge Cases

- **Catalog is large (~46 models observed in M0d/DEC-011)**: the list renders responsively and search/filter narrowing stays usable; the screen does not require fetching or downloading any model content to browse.
- **A model is cached but its enriched metadata is partially unavailable**: the cached badge is still accurate and missing fields render "unknown / not provided" — the two concerns are independent.
- **Cached state changes underneath the view** (e.g., another process): M2 is read-only discovery; a manual refresh re-reads cached/available state honestly (no auto-mutation, no download triggered by browsing).
- **Filter facet has no members** (e.g., no NPU models in the current catalog): selecting it yields the honest empty state, not an error.
- **FL provides a device/task/provider value M2 does not recognize**: it is shown honestly (passed through / labeled unknown) rather than dropped or mislabeled into a known facet.
- **Search and filters combined to an empty intersection**: the empty state distinguishes "no match for current search+filters" and a reset restores the curated default.
- **DevFlow screenshot cannot capture the WKWebView layer (KI-001)**: visual verification of the catalog relies on DOM inspection + a human eyeball, not `ui screenshot`; this is the accepted verification path for M2, not a defect.
- **Browsing must never download or load**: no card render, hover, filter, or search may trigger an FL `DownloadAsync`/`LoadAsync`; discovery is strictly non-mutating.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The app MUST present a catalog browse screen as the first real end-user surface, rendered only after the M1 ready-gate has transitioned the app to the "ready" state (the screen is blocked by the M1 "initializing" guard until then).
- **FR-002**: The catalog screen MUST obtain all model data exclusively through the FL-free `IFoundryCatalogService` seam; the UI MUST NOT reference or call the Foundry Local SDK directly (Constitution V; DEC-004).
- **FR-003**: The screen MUST render one per-model card for each model in the active view, sourced from the catalog service's browse/list operations.
- **FR-004**: Each card MUST display an explicit, visually-and-semantically-distinct cached-vs-not-cached badge whose value reflects the model's real cached state; cached state MUST NOT be conveyed by color alone.
- **FR-005**: The screen MUST provide a search input that narrows the catalog to models whose alias, display name, or id contains the query case-insensitively, wired to `CatalogFilter.SearchText` and the existing `CatalogFilterExtensions` predicate (no duplicate/parallel filtering logic).
- **FR-006**: The screen MUST provide filters for device (CPU/GPU/NPU), task, and provider, each wired to the corresponding `CatalogFilter` field (`Device`/`Task`/`Provider`), and a "cached only" toggle wired to `CatalogFilter.CachedOnly`.
- **FR-007**: Search and all filters MUST combine as an intersection (logical AND), consistent with the existing `CatalogFilterExtensions.Matches` behavior, and the screen MUST offer a discoverable reset/clear affordance that restores the default view.
- **FR-008**: The screen MUST present a curated/recommended default view (a subset surfaced first), clearly labeled as curated and distinct from the full list, with a single discoverable action to view the full catalog; the curated selection rule MUST be deterministic and MUST NOT make fabricated quality claims unsupported by FL metadata.
- **FR-009**: Search and filtering MUST always apply to the full catalog, never silently confined to only the curated subset.
- **FR-010**: Each card MUST display, where available, the model's alias, display name, size (GB), device / execution provider, context length, capabilities (e.g. vision / tool / reasoning), license, cached badge, and variant availability (count or list of quant/device variants).
- **FR-011**: The catalog service's per-model metadata MUST be enriched from real Foundry Local model metadata — replacing the M1 `MapBasic` stub (`SizeGb: 0`, hardcoded `Device.Gpu`, empty `Task`/`Provider`/`Variants`) — mapping at least: real size, device/execution provider, task, provider, capabilities, license, context length, variants, and cached/loaded state (resolving the `TODO(M2)` markers in `FoundryCatalogService.cs`).
- **FR-012**: Where a metadata field is genuinely unavailable from Foundry Local for a given model, the card MUST render an honest "unknown / not provided" indicator (or honestly omit the field) and MUST NOT show a fabricated, defaulted, or stand-in value such as `0 GB` for a real size (Constitution IV capability honesty).
- **FR-013**: M2 MUST be discovery only: the screen MUST NOT provide any affordance to download, load, unload, delete, select-a-variant-to-load, or chat; no browse/search/filter/render action may trigger a Foundry Local download or load (those are M3/M4).
- **FR-014**: The screen MUST present an honest loading state while the app is reaching ready and/or the catalog is being fetched, distinct from an empty list.
- **FR-015**: The screen MUST present a distinct "no models match" empty state when a search/filter combination matches nothing, with a reset/clear affordance, visually and semantically distinct from the loading and error states.
- **FR-016**: The screen MUST present an honest, actionable error state with a retry when the catalog cannot be loaded (FL unavailable or catalog error), surfacing the diagnosed cause rather than a generic failure, and MUST NOT present a fetch failure as a silent empty (zero-models) result.
- **FR-017**: The catalog MUST be refreshable on demand (a manual refresh that re-reads available/cached state honestly); refreshing MUST NOT download, load, or otherwise mutate model state.
- **FR-018**: The catalog screen and all of its controls, cards, badges, and states MUST meet WCAG AA by default: keyboard operable, labeled for assistive technology, sufficient contrast, and no information conveyed by color alone.
- **FR-019**: The filter and search behavior MUST remain covered by pure-logic unit tests over `CatalogFilter`/`CatalogFilterExtensions` that run with no native Foundry Local dylib present, and the new metadata-mapping logic MUST be testable from FL-metadata fixtures without a dylib.
- **FR-020**: M2 MUST NOT introduce UI or toggles for unsupported Foundry Local capabilities and MUST NOT include M3+ scope (download/progress/cache-management/delete, variant selection to load, streaming chat, the exposed server, RAG/voice/presets).
- **FR-021**: Any workaround for an upstream gap used in M2 MUST be recorded with a tracking link in `KNOWN-ISSUES.md` and removed when the upstream fix lands; M2 MUST honor KI-001 (verify Blazor-on-AppKit visual state via DOM inspection plus human eyeball, not `ui screenshot`).
- **FR-022**: M2 SHOULD harden the M1 lifecycle dispose path deferred as KI-008 (volatile `_disposed`, dispose under the init lock, continuation-dispose for late-completing init) since KI-008 is explicitly assigned to "M2 hardening"; if deferred, the deferral MUST be recorded honestly in the closing note.
- **FR-023**: M2 MUST close with a real Apple-Silicon DevFlow end-to-end check (catalog launch → ready → browse/search/filter, verified via DOM inspection and screenshots subject to the KI-001 caveat), and the milestone-closing note MUST end with a `Verified:` line naming the checks that ran (Constitution II).
- **FR-024**: The original author of the M2 change set MUST NOT be its sole approver; reviewer independence MUST be preserved (Constitution III).

### Key Entities *(include if feature involves data)*

- **Catalog view model**: the ordered set of per-model cards currently displayed, derived by applying the active `CatalogFilter` (and curated/full selection) to the enriched catalog; the unit of what the user sees.
- **Per-model card**: the display representation of a single model — alias, display name, size (GB), device / execution provider, context length, capabilities, license, cached badge, variant availability — each field either a real FL value or an honest "unknown / not provided".
- **Enriched ModelInfo**: the M2-populated `ModelInfo` (Core record) whose previously-stubbed fields (`SizeGb`, `Device`, `Task`, `Provider`, `Variants`, `IsCached`, `IsLoaded`) now carry real Foundry Local metadata mapped through the catalog service.
- **Model variant availability**: the count/list of quant/device variants (`ModelVariant`) exposed for informational display only (no selection-to-load in M2).
- **CatalogFilter (existing seam)**: the criteria record (`Device`/`Task`/`Provider`/`SearchText`/`CachedOnly`) that the search box, filters, and cached-only toggle bind to; matching stays in `CatalogFilterExtensions`.
- **Curated default selection**: the deterministic recommended subset surfaced first, distinct from the full catalog, with a discoverable switch to "all models".
- **Catalog screen state**: the mutually-exclusive UI state — loading, populated, empty (no match), or error (fetch failed) — each rendered honestly and accessibly.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a cold launch on an Apple Silicon Mac, after the app reaches "ready" the user sees a populated catalog of per-model cards (one card per available Foundry Local model) with no manual configuration, verified via DevFlow DOM.
- **SC-002**: 100% of rendered cards show a cached-vs-not-cached badge whose value matches the model's actual cached state (cross-checked against the cached list); 0 cards convey cached state by color alone.
- **SC-003**: Entering a search query narrows the visible cards to exactly the models whose alias, display name, or id matches case-insensitively, and the same narrowing is reproduced by a pure-logic unit test over `CatalogFilter`/`CatalogFilterExtensions` running with no native dylib.
- **SC-004**: Applying any device/task/provider filter or the cached-only toggle reduces the list to exactly the matching set, and combined search+filters intersect (AND) identically to the `CatalogFilterExtensions.Matches` predicate (0 discrepancies across the test suite).
- **SC-005**: On a fresh launch with no search/filter, a curated subset is shown first and labeled as curated, and a single discoverable action reveals the full catalog; search/filter always operates over the full catalog (verified by searching for a non-curated model and finding it).
- **SC-006**: For at least the M0d-known model set, every present card field reflects real Foundry Local metadata (size, device/EP, context length, capabilities, license, variants) — 0 cards show the M1 stub defaults (`0 GB`, hardcoded GPU, empty task/provider/variants) for a model where FL actually provides those values.
- **SC-007**: Every metadata field FL genuinely does not provide renders an explicit "unknown / not provided" indicator; 0 fabricated or defaulted stand-in values appear (no `0 GB` presented as a real size).
- **SC-008**: 0 download, load, unload, delete, variant-select-to-load, or chat affordances exist on the M2 screen, and across a full browse/search/filter session 0 Foundry Local download or load operations are triggered (verified: cached state of non-cached models is unchanged after browsing).
- **SC-009**: Each of the three honest states is observable and distinct: a loading state during init/fetch, a "no match" empty state for an over-constrained filter, and an actionable error state with retry on a simulated/real catalog fetch failure (the failure is never shown as a silent empty list).
- **SC-010**: The catalog screen passes WCAG AA checks: all controls, cards, badges, and states are keyboard-operable and announced by assistive technology, with sufficient contrast and no color-only information (verified via DOM/accessibility inspection).
- **SC-011**: The M2 closing note ends with a `Verified:` line from a real Apple-Silicon DevFlow end-to-end browse/search/filter check (DOM inspection + screenshots subject to the KI-001 caveat), and the change set was approved by someone other than its author.
- **SC-012**: 0 UI or toggles exist for unsupported FL capabilities or for M3+ scope; any M2 workaround is recorded with a tracking link in `KNOWN-ISSUES.md`, and the KI-008 dispose hardening is either completed or its deferral recorded honestly.

## Assumptions

- The foundation is M1-complete on `net11.0-macos` (DEC-016): the AppKit + Blazor Hybrid head, the single `FoundryLocalManager` behind the off-dispatcher `ReadyAsync` gate (`AppReadyBoundary` "initializing"→"ready"), the load/unload concurrency gate, the `IFoundryCatalogService` seam, the `CatalogFilter`/`CatalogFilterExtensions` pure-logic seam, settings, and the dylib-free test/CI seam all exist and are proven. M2 builds on this and does not re-litigate it.
- The architecture follows DEC-004: macOS / Apple Silicon only, `net11.0-macos`, Blazor Hybrid UI on the AppKit head, all model data through the FL-free `IFoundryCatalogService` seam (UI never touches the FL SDK directly), and the single manager via the M1 ready-gate.
- The real Foundry Local model metadata available for honest enrichment includes (per the FL `1.2.3` `ModelInfo` surface) alias, display name, version, file size, device type, execution provider, task, provider type/publisher, license, context length, max output tokens, prompt template, runtime, capabilities, and tool-calling support; M2 maps the card's fields from these and renders "unknown / not provided" wherever a value is genuinely absent. (Exact capability flags such as vision/reasoning are surfaced only where FL actually exposes them.)
- "Curated default view" is a deterministic, app-defined recommended subset (e.g., a small recommended/popular set surfaced first) — it makes no fabricated quality claims and the full catalog is always one action away; the exact curated list is an implementation detail to be chosen honestly during planning.
- M2 is browse/discovery only; per the PLAN.md scope split, download-with-progress/cache-management/delete and variant-selection-to-load are M3, streaming chat is M4, the exposed local server is M5, and RAG/voice/presets are post-v1 — none are in M2.
- The "memory fit indicator" noted on the PLAN.md M2 line is treated as a stretch/optional honest refinement (size-vs-free-RAM, never a confident green verdict); the RAM-fit pure-logic seam already exists (`RamFitResult`) from M1, but M2's core scope is browse/search/filter/cards and a confident fit verdict is explicitly out of scope. If surfaced at all, it is an honest "size vs free RAM" indicator with a "long chats use more" caveat, not a will-it-run guarantee.
- Verification follows KI-001: Blazor-on-AppKit visual state is verified via DevFlow DOM inspection (`webview source` / `Runtime evaluate`) plus a human eyeball; `ui screenshot` does not capture the WKWebView layer and is not relied upon for WebView content. Screenshots are still attached where useful for the milestone record.
- The canonical references for M2 scope and sequencing are `docs/PLAN.md` (the M2 milestone, the LM Studio parity-map catalog/variant rows, the competitive-positioning section), the constitution, `.squad/decisions.md` (DEC-004, DEC-014, DEC-016), `KNOWN-ISSUES.md` (KI-001, KI-007, KI-008), the completed M1 spec, and the real code under `src/` (`ModelInfo`/`ModelVariant`/`CatalogFilter`, `CatalogFilterExtensions`, `IFoundryCatalogService`, `FoundryCatalogService` with its `TODO(M2)` enrichment markers).
- "Independent tests" for the screen-bearing stories are framed around observable UI behavior verifiable via DevFlow (DOM inspection + the KI-001 screenshot caveat) and the pure-logic seams (filter predicates, metadata mapping) that are unit-testable without a dylib; reviewer independence and the closing `Verified:` line are mandatory per the constitution.
