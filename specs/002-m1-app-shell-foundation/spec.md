# Feature Specification: M1 — App Shell + Foundry Local Service Layer + DI + Test/CI Seam

**Feature Branch**: `002-m1-app-shell-foundation`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "M1 establishes the production app skeleton for FoundryStudio (native macOS AppKit head + Blazor Hybrid). It replaces the disposable M0 spikes with the real solution structure and the Foundry Local service layer that every later milestone depends on. M1 delivers no end-user 'wow' feature; its value is a correct, tested, DI-wired foundation."

## Overview

M0 is DONE and GO: the foundation — net10.0-macos AppKit head + Blazor Hybrid, the Foundry Local native dylib chain bundling/signing/loading in-process, and a streamed vertical slice — is proven on real Apple Silicon. M1 replaces the throwaway M0 spikes with the **real, non-throwaway application skeleton** and the Foundry Local service layer that M2 (catalog), M3 (model management), M4 (chat), and M5 (server) all build on.

M1 ships **no end-user "wow" feature**. Its entire value is a correct, tested, dependency-injection-wired foundation: a properly scaffolded solution, a single Foundry Local lifecycle manager behind a ready-gate that every consumer awaits, the singleton load/unload concurrency contract, the service interfaces (with in-process implementations where M1 needs them and stubs for post-v1 surfaces), a user-editable settings store, and a unit-test project plus a clean-checkout CI build that defends the pinned multi-preview stack against silent churn.

Because this is a foundation/library feature rather than a screen-bearing one, success is framed around **verifiable service behavior** — ready-state reached without deadlock, the concurrency gate draining or rejecting correctly, settings persisted and never silently wiped, and CI building clean on pinned versions — not around end-user UI.

## Clarifications

No outstanding clarifications. All open choices were resolved using reasonable defaults derived from `docs/PLAN.md`, the constitution, `KNOWN-ISSUES.md` (KI-001…KI-006), and the completed M0 spec; the resulting defaults are recorded in the **Assumptions** section.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ready-gated Foundry Local lifecycle manager + DI so the app launches and reaches a ready state (Priority: P1)

As the FoundryStudio developer, I need the real app to start, wire a single Foundry Local lifecycle service through dependency injection, run its heavy native initialization off the UI thread behind a ready-gate that every consumer awaits, and reach a well-defined "ready" state without freezing or deadlocking the UI — so that every later milestone can depend on a correctly initialized Foundry Local instance.

**Why this priority**: Nothing else in the app can function until Foundry Local is initialized exactly once, off the dispatcher thread, behind a gate that downstream services await. This is the spine of the whole architecture; getting it wrong reintroduces the UI-freeze and deadlock failure modes M0 diagnosed (KI-005). It is the first thing that must exist and be testable.

**Independent Test**: Launch the real app skeleton on an Apple Silicon Mac; observe an app-level "initializing" state that blocks the chat surface, then a transition to "ready" once initialization completes; confirm via behavior tests that the ready-gate offloads initialization to a background thread, that consumers awaiting the gate all receive the same single initialized instance, and that no code path blocks the init task with a synchronous wait.

**Acceptance Scenarios**:

1. **Given** a clean launch of the app skeleton, **When** the app starts, **Then** Foundry Local initialization runs off the UI dispatcher thread and the app shows an "initializing" state that prevents use of not-yet-ready surfaces until initialization completes.
2. **Given** initialization completes, **When** any service or component awaits the ready-gate, **Then** it receives the one shared initialized Foundry Local instance and the app transitions to a "ready" state with UI updates marshalled back onto the dispatcher.
3. **Given** multiple consumers await the ready-gate concurrently before initialization completes, **When** initialization finishes, **Then** initialization has run exactly once and all consumers observe the same instance (no duplicate managers, no second initialization).
4. **Given** the running app, **When** the app exits, **Then** the Foundry Local manager is disposed cleanly as part of shutdown.
5. **Given** the codebase, **When** it is inspected for blocking calls on the initialization task, **Then** no synchronous blocking wait on the init task exists anywhere (the ready-gate is awaited, never blocked on).

---

### User Story 2 - Singleton load/unload concurrency gate (Priority: P1)

As the FoundryStudio developer, I need a single Foundry Local manager — the one that backs both the in-process UI path and the later externally-exposed server — to enforce a load/unload concurrency contract that drains or rejects in-flight generations before mutating model state, so that a model is never loaded or unloaded while a generation is actively streaming on it (which would tear the generation or crash natively).

**Why this priority**: There is exactly one Foundry Local manager backing two surfaces (UI now, exposed server in M5). A load/unload that races an active stream is a native crash, not a recoverable error. The plan and constitution require this contract to be designed and implemented in M1, not discovered in M5. It is pure-logic concurrency behavior that can be built and tested independently of any UI.

**Independent Test**: Through the service layer alone (no UI), start a simulated in-flight generation on a model, then request a load/unload of that model; confirm the gate either waits for the generation to drain or rejects the mutation with a clear, honest signal — and that load/unload never proceeds concurrently with an active stream on the same model. Confirm concurrent load/unload requests are serialized.

**Acceptance Scenarios**:

1. **Given** an active generation streaming on a loaded model, **When** an unload (or load that would displace it) is requested for that model, **Then** the operation either waits until the generation drains or is rejected, and never mutates model state mid-stream.
2. **Given** no active generation on a model, **When** a load or unload is requested, **Then** the operation proceeds and completes.
3. **Given** two model-state mutations requested concurrently, **When** the gate processes them, **Then** they are serialized so the manager's model state is never mutated by two operations at once.
4. **Given** a mutation is rejected because a stream is active, **When** the rejection is surfaced, **Then** it is an honest, actionable signal (not a fake success and not a silent no-op).
5. **Given** the single manager backs both the in-process path and the (future) exposed server, **When** the gate is exercised, **Then** the same contract governs requests arriving from either surface.

---

### User Story 3 - Test project + CI seam from day one (Priority: P1)

As the FoundryStudio developer, I need a unit-test project that covers the pure-logic seams behind the Foundry Local service interfaces (settings, catalog filtering, the RAM-fit heuristic) without requiring the native dylib, plus one CI job that restores and builds the whole solution on a clean checkout against the pinned known-good versions — so that the multi-preview stack is defended against silent churn and the foundation's logic is verifiable on every change.

**Why this priority**: The constitution forbids an engineer ever seeing a red CI X, and the plan mandates tests + CI "from day one." A pinned four-preview stack drifts silently without a clean-checkout build gate. The testable seams must be designed to need no native dylib so they run anywhere, including CI. This is P1 because it is the guardrail that keeps the foundation honest from the first commit.

**Independent Test**: On a clean checkout, run the CI job and confirm it restores and builds the entire solution using only the pinned versions (no floating to "latest"); run the unit-test project and confirm it exercises the settings, catalog-filtering, and RAM-fit-heuristic logic seams and passes without any native Foundry Local dylib present.

**Acceptance Scenarios**:

1. **Given** a clean checkout, **When** the CI job runs, **Then** it restores and builds the full solution using only the pinned known-good versions and fails if any dependency resolves to an unpinned version.
2. **Given** the unit-test project, **When** it runs in an environment with no native Foundry Local dylib, **Then** the pure-logic seam tests (settings, catalog filtering, RAM-fit heuristic) execute and pass.
3. **Given** a change that breaks a pure-logic seam, **When** CI runs, **Then** the failing test is reported and the job fails (the seam is genuinely covered, not a placeholder).
4. **Given** a dependency silently changes version, **When** CI restores on a clean checkout, **Then** the deviation from the pinned set is surfaced as a failure rather than silently accepted.

---

### User Story 4 - Catalog service + in-process chat-client adapter (Priority: P2)

As the FoundryStudio developer, I need a catalog service that wraps Foundry Local's catalog and model operations (browse, download, load, unload, delete, variants, and the cached/loaded lists) behind a stable interface, plus a thin in-process chat-client adapter over the Foundry Local SDK (no loopback socket) — so that M2's catalog UI and M4's chat UI build against stable seams instead of the SDK directly, and chat composes standard middleware without a network hop.

**Why this priority**: These are the primary service seams M2 and M4 consume, but they depend on Story 1's ready-gate and Story 2's concurrency gate existing first. The catalog service and chat adapter are the "real work" surfaces; defining them now lets later milestones move fast. They are P2 because the foundation (Stories 1–3) must exist before these seams are meaningful.

**Independent Test**: Through the service layer (no UI), exercise the catalog service's operations against the ready-gated manager and confirm each maps to the correct Foundry Local operation and respects the concurrency gate for load/unload; confirm the chat adapter exposes a standard in-process chat-client surface over the SDK with no loopback socket, so middleware (function-invocation, telemetry) can compose around it.

**Acceptance Scenarios**:

1. **Given** the ready-gated manager, **When** a consumer calls the catalog service to browse, list cached models, or list loaded models, **Then** the service returns results sourced from Foundry Local's catalog/model operations behind the interface.
2. **Given** a load or unload requested through the catalog service, **When** it executes, **Then** it routes through the singleton concurrency gate (Story 2) and never mutates model state mid-stream.
3. **Given** a model with multiple variants, **When** the catalog service is asked for that model, **Then** its variants are exposed so a later UI can offer variant selection.
4. **Given** a loaded model, **When** a consumer requests a chat completion through the in-process chat-client adapter, **Then** the request is served in-process over the Foundry Local SDK with no loopback HTTP socket involved.
5. **Given** the chat adapter, **When** standard middleware is composed around it, **Then** the adapter presents a conventional chat-client surface that supports such composition.

---

### User Story 5 - App settings / persistence store (Priority: P2)

As the FoundryStudio user, I need my app settings (model cache directory, default model, theme) persisted across launches, fully user-editable, and never wiped or rewritten without my consent — so that my configuration and the multi-gigabyte model cache it points at are treated as protected user data.

**Why this priority**: Settings are needed by several later milestones (cache directory for M3, default model and theme for M4/M6), and the constitution's data-preservation rule makes "never wiped without consent" non-negotiable. It is P2 because the core lifecycle/concurrency/CI foundation must land first, but it is a small, well-bounded seam that unblocks later work.

**Independent Test**: Through the settings store (no UI), write the cache directory, default model, and theme; restart the process and confirm the values persist; confirm all writes are auditable and user-editable and that no operation silently wipes or overwrites stored settings without an explicit consent step.

**Acceptance Scenarios**:

1. **Given** a fresh install, **When** settings are read before any have been set, **Then** sensible documented defaults are returned (including a default model cache directory).
2. **Given** a user sets the model cache directory, default model, or theme, **When** the app is restarted, **Then** the persisted values are read back unchanged.
3. **Given** persisted settings exist, **When** any operation would clear or overwrite them destructively, **Then** it does not proceed without explicit per-action consent, and persisted state is never silently wiped.
4. **Given** the persistence store, **When** its contents are inspected, **Then** they are human-auditable and user-editable rather than opaque.

---

### Edge Cases

- **Initialization fails or is slow**: the app remains in the "initializing" state and surfaces an honest, diagnosed cause (not a generic spinner forever and not a fake "ready"); not-yet-ready surfaces stay blocked. The ready-gate must never be satisfied by a failed initialization.
- **A consumer awaits the ready-gate after initialization already completed**: it returns the already-initialized instance immediately without re-initializing.
- **Load/unload requested during an active stream on the same model**: the concurrency gate drains or rejects per Story 2; it never proceeds concurrently.
- **Load/unload requested during an active stream on a *different* model**: allowed, since the contract protects per active-stream model state, not all model operations globally.
- **App exit while initialization is still in flight**: shutdown disposes the manager without deadlocking on the in-flight init task (no synchronous blocking wait).
- **CI runs where a transitive dependency floats off the pinned set**: CI fails rather than silently building against an unpinned version.
- **Unit tests run with no native dylib present (e.g., CI Linux/host agent)**: only the pure-logic seams run there; native-dependent behavior is not required for those tests to pass.
- **Settings file is missing, empty, or corrupt on read**: documented defaults are applied and the user's prior data is not destroyed without consent; recovery is non-destructive.
- **Post-v1 service interfaces are referenced before they are implemented**: embedding, transcription, and local-server interfaces exist with stubbed implementations that clearly signal "not implemented in v1" rather than faking behavior.
- **Capability honesty for structured output**: structured-output support is best-effort-only (per M0d); the foundation must not design or promise a "guaranteed JSON" capability or any UI/toggle for an unsupported Foundry Local feature.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The solution MUST be scaffolded as the real, non-throwaway application structure — an AppKit host project, a Razor UI surface, and shared services/models — replacing the disposable M0 spikes.
- **FR-002**: The Blazor imports MUST be generated from the proper MAUI Blazor template set and MUST include the core component usings (components, web, forms, routing, and JS-interop) so that event handlers and bindings compile as interactive (per KI-006; a missing core using silently breaks `@onclick`/`@bind` with no build error).
- **FR-003**: There MUST be exactly one Foundry Local lifecycle manager, registered for dependency injection and shared by every consumer; no code path may construct a second manager.
- **FR-004**: The manager MUST expose a ready-gate that every consumer awaits before using Foundry Local, and an app-level "initializing" route/guard MUST block not-yet-ready surfaces (including the chat surface) until the gate is satisfied.
- **FR-005**: Foundry Local initialization MUST run off the Blazor dispatcher thread (on a background task), and UI updates MUST be marshalled back onto the dispatcher (per KI-005).
- **FR-006**: No code path may block on the initialization task with a synchronous wait; the ready-gate is awaited, never blocked on (hard rule per KI-005).
- **FR-007**: The Foundry Local manager MUST be disposed cleanly on app exit.
- **FR-008**: A load/unload concurrency gate MUST drain or reject in-flight generations before mutating model state, and MUST never load or unload a model while a generation is actively streaming on that model.
- **FR-009**: The concurrency gate MUST serialize concurrent model-state mutations so the manager's model state is never mutated by two operations simultaneously, and MUST govern requests arriving from both the in-process UI path and the future exposed server (one manager backs both).
- **FR-010**: A rejection by the concurrency gate MUST be surfaced as an honest, actionable signal — never a fake success or a silent no-op.
- **FR-011**: A catalog service interface MUST wrap Foundry Local's catalog/model operations — browse, download, load, unload, delete, variants, and the cached and loaded lists — and its load/unload paths MUST route through the concurrency gate.
- **FR-012**: A thin in-process chat-client adapter over the Foundry Local SDK MUST be provided, presenting a conventional chat-client surface that standard middleware can compose around, with NO loopback HTTP socket (the exposed server is for external tools only and is out of M1 scope).
- **FR-013**: Interface definitions MUST exist for the post-v1 embedding, transcription, and local-server services, with their implementations stubbed in a way that clearly signals "not implemented in v1" rather than faking behavior.
- **FR-014**: An app settings/persistence store MUST persist at least the model cache directory, the default model, and the theme across launches, and MUST return documented defaults when values are unset, missing, or unreadable.
- **FR-015**: All persistent settings writes MUST be auditable and user-editable, and persisted settings (and the model cache they reference) MUST NOT be wiped or destructively overwritten without explicit per-action consent (constitution: data preservation).
- **FR-016**: A unit-test project MUST cover the pure-logic seams behind the Foundry Local service interfaces — at minimum the settings store, catalog filtering, and the RAM-fit heuristic — and these tests MUST run with no native Foundry Local dylib present.
- **FR-017**: One CI job MUST restore and build the entire solution on a clean checkout using only the pinned known-good versions, and MUST fail if a dependency resolves off the pinned set or if the build or seam tests fail.
- **FR-018**: The foundation MUST NOT design, implement, or expose any UI/toggle for capabilities Foundry Local does not support, and structured-output support MUST be treated as best-effort-only (no "guaranteed JSON" promise, per M0d and the constitution's capability-honesty rule).
- **FR-019**: M1 MUST NOT include the catalog browse UI (M2), model management UI (M3), streaming chat UI (M4), the exposed local server toggle (M5), or any post-v1 feature (RAG, voice, presets, MCP); the net11 toolchain pin is a separate chore and is out of M1 scope.
- **FR-020**: Any workaround applied to an upstream gap during M1 MUST be recorded with a tracking link in the known-issues reference and removed when the upstream fix lands; KI-005 and KI-006 MUST be codified into the M1 service layer and app scaffold respectively.
- **FR-021**: M1 MUST close with a real Apple-Silicon end-to-end check that the app launches and reaches the ready state, and the milestone-closing note MUST end with a `Verified:` line naming the checks that ran (constitution: pre-completion verification).
- **FR-022**: The original author of a change MUST NOT be the sole approver of that change; reviewer independence MUST be preserved (constitution).

### Key Entities *(include if feature involves data)*

- **Foundry Local lifecycle manager (singleton)**: the single instance that owns Foundry Local initialization, the ready-gate, the concurrency gate, and disposal; backs both the in-process UI path and the future exposed server.
- **Ready-gate**: the awaitable readiness contract every consumer awaits; satisfied exactly once on successful initialization, never by a failed init, and never blocked on synchronously.
- **Concurrency gate / model-state mutation**: the contract that serializes load/unload operations and drains-or-rejects them against any active stream on the same model.
- **Catalog service**: the interface wrapping browse/download/load/unload/delete/variants and the cached/loaded lists over Foundry Local's catalog and model operations.
- **In-process chat-client adapter**: the thin adapter presenting a conventional chat-client surface over the SDK with no socket, around which middleware composes.
- **Post-v1 service interfaces (stubbed)**: embedding, transcription, and local-server interfaces defined now with non-faking stub implementations.
- **App settings record**: the user-editable, auditable persisted configuration — model cache directory, default model, theme — protected as user data.
- **Pinned version set**: the known-good package/SDK versions the CI clean-checkout build is gated against.
- **Pure-logic test seam**: a unit-testable behavior (settings, catalog filtering, RAM-fit heuristic) that requires no native dylib.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The real app skeleton launches on an Apple Silicon Mac, runs Foundry Local initialization off the UI thread, shows an "initializing" state, and transitions to a "ready" state with the chat surface unblocked only after the ready-gate is satisfied.
- **SC-002**: Across all consumers, Foundry Local is initialized exactly once and exactly one manager instance is shared; no second manager is ever constructed.
- **SC-003**: There is zero synchronous blocking on the initialization task anywhere in the codebase, and the app never freezes or deadlocks the UI during initialization (no reproduction of the KI-005 freeze).
- **SC-004**: In service-level tests, a load/unload requested against a model with an active stream is always either drained-then-applied or rejected — never applied mid-stream — and concurrent mutations are always serialized (0 observed concurrent mutations across the test suite).
- **SC-005**: The catalog service exposes browse, download, load, unload, delete, variants, and cached/loaded lists, with load/unload verifiably routed through the concurrency gate.
- **SC-006**: A chat completion can be obtained through the in-process chat-client adapter with no loopback socket opened (0 network sockets bound for the in-process chat path).
- **SC-007**: Settings (cache directory, default model, theme) persist across a process restart with 100% fidelity, and no test or code path wipes persisted settings without an explicit consent step.
- **SC-008**: The unit-test project's pure-logic seam tests (settings, catalog filtering, RAM-fit heuristic) pass in an environment with no native Foundry Local dylib present.
- **SC-009**: The CI job restores and builds the full solution from a clean checkout on the pinned versions, and fails if any dependency floats off the pinned set or any seam test fails.
- **SC-010**: No UI or toggle exists for any unsupported Foundry Local capability, and structured output is represented as best-effort-only with no "guaranteed JSON" promise.
- **SC-011**: The M1 closing note ends with a `Verified:` line from a real Apple-Silicon end-to-end launch-to-ready check, and the change set was approved by someone other than its author.
- **SC-012**: KI-005 (off-dispatcher init, no synchronous blocking) and KI-006 (proper Blazor imports) are codified in the M1 service layer and app scaffold, and any new M1 workaround has a tracking link in the known-issues reference.

## Assumptions

- The proven baseline is `net10.0-macos` (M0 GO); the net11 toolchain pin is a **separate open chore (T004)** and is explicitly out of M1 scope. M1 builds on the proven baseline rather than blocking on net11.
- The architecture follows `docs/PLAN.md`: in-process Foundry Local SDK for catalog/model/chat via a thin chat-client adapter (no loopback socket), with one manager singleton governed by a load/unload concurrency gate; the exposed OpenAI-compatible server is for external tools only and is delivered later (M5).
- The MAUI dependency-injection container is the service registration mechanism, and Razor components call injected services directly (Blazor Hybrid is in-process; no server circuit).
- Settings persistence uses the platform preferences mechanism plus a JSON representation that is human-auditable and user-editable; the model cache directory it stores points at multi-gigabyte protected user data.
- The RAM-fit heuristic is a pure-logic seam (e.g., model size vs. free RAM with margin) that needs no native dylib to test; its UI presentation and any "fit" badge belong to M2, not M1.
- The post-v1 service interfaces (embedding, transcription, local-server) are defined in M1 with non-faking stubs so later milestones can implement them without reshaping the dependency graph.
- "Tests + CI from day one" means the unit-test project and the clean-checkout build job exist and run in M1; broader test coverage (catalog UI, chat, server) lands with the milestones that introduce those features.
- The canonical reference for M1 scope and sequencing is `docs/PLAN.md` (the M1 milestone, Architecture, and Key integration notes) together with the constitution, `KNOWN-ISSUES.md` (KI-001…KI-006), `KNOWN-GOOD-VERSIONS.md`, and the completed M0 spec.
- MacOS / Apple Silicon only; models are ONNX-only; pinned known-good versions are not floated; reviewer independence and the closing `Verified:` line are mandatory per the constitution.
- This is a foundation/library feature: "independent tests" are framed around verifiable service behavior (ready-state reached, no synchronous-wait deadlock, concurrency gate drains/rejects correctly, settings persist without silent wipe, CI builds clean on pinned versions) rather than end-user screens.
