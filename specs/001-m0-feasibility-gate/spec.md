# Feature Specification: M0 Feasibility Gate

**Feature Branch**: `001-m0-feasibility-gate`

**Created**: 2026-06-24

**Status**: Draft

**Input**: User description: "the M0 feasibility gate"

## Overview

M0 is the linchpin go/no-go gate for FoundryForge. Before any application code is built, the team must prove — on a real Apple Silicon Mac — that the chosen foundation actually works: that a Foundry Local-powered, Blazor Hybrid UI hosted on the maui-labs AppKit macOS head can build, load the Foundry Local native dylib chain in-process, render rich UI, and stream a real model reply.

M0 is staged into four sequential sub-gates (M0a → M0b → M0c → M0d). Each gate is independently demonstrable and fails cheap. A failed gate stops forward progress and triggers a recorded go/no-go decision before any later gate or milestone (M1+) begins. The deliverable of M0 is **a decision and the evidence behind it**, not shippable product features.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Toolchain and stack baseline proven (M0a) (Priority: P1)

As the FoundryForge developer, I need to confirm that the pinned `net11.0-macos` AppKit + Blazor Hybrid package set builds and launches an empty, Sherpa-shaped app on my Mac, so that all later feasibility work stands on a known-good, version-pinned foundation rather than shifting preview packages.

**Why this priority**: Every later gate depends on a buildable, launchable baseline. It is the cheapest gate and must pass first; if the net11 preview set is unstable, the team falls back to the proven net10 reference set before spending effort on native-load work.

**Independent Test**: Create a minimal AppKit + BlazorWebView app on the pinned versions, run it via the standard local build-and-run, and confirm a window opens and renders a trivial Blazor page. No Foundry Local involved.

**Acceptance Scenarios**:

1. **Given** the pinned version set recorded in the known-good versions reference, **When** the developer restores and builds an empty AppKit + BlazorWebView app on a clean checkout, **Then** the build completes without errors using only the pinned versions.
2. **Given** a successful build, **When** the developer launches the app on an Apple Silicon Mac, **Then** a native window opens and a trivial Blazor page renders.
3. **Given** the net11 preview AppKit set proves unstable, **When** the developer evaluates the fallback, **Then** the decision to use the net10 reference set (or to proceed on net11) is recorded with its rationale.

---

### User Story 2 - Foundry Local native-load proven in a console head (M0b) (Priority: P1)

As the FoundryForge developer, I need to prove that the complete Foundry Local native dylib chain can be bundled into the macOS app package, resolve its inter-library references, load in-process, and run one inference — in a minimal console head with no UI — so that the project's single biggest unknown is resolved before any UI is built.

**Why this priority**: This is the project's true go/no-go. Foundry Local is a dylib *chain* (the core library plus the ONNX Runtime, ONNX Runtime GenAI, and Dawn dependencies) loaded into the process, unlike the single executable that the reference app bundles. If the chain cannot load under macOS library validation, the entire architecture is invalid and the project does not proceed.

**Independent Test**: In a minimal macOS console project with the Foundry Local SDK added, build, confirm the whole dylib chain lands inside the app package and its references resolve, then initialize Foundry Local in-process and complete one inference — all without any MAUI or Blazor code.

**Acceptance Scenarios**:

1. **Given** a minimal console head with the Foundry Local SDK referenced, **When** the developer builds it, **Then** the complete Foundry Local native dylib chain is present inside the app package's bundled native runtime location.
2. **Given** the bundled app package, **When** the developer inspects each bundled dylib's linkage, **Then** every inter-library reference in the chain resolves within the bundle.
3. **Given** the bundled, signed app, **When** the developer initializes Foundry Local in-process and requests one inference, **Then** the model loads and returns a valid completion without a native crash or library-validation failure.
4. **Given** the in-process load fails library validation, **When** the developer applies the documented entitlement and nested-dylib re-signing remedy, **Then** either load succeeds and the required entitlement is recorded, or the gate is declared no-go with evidence.

---

### User Story 3 - BlazorWebView capability probe (M0c) (Priority: P2)

As the FoundryForge developer, I need to confirm which web-view-hosted UI capabilities are available on the AppKit head — specifically local file intake into the page and hot reload during development — so that post-v1 features that depend on file intake are de-risked now and the inner development loop is known to work.

**Why this priority**: Rich Blazor Hybrid UI on the AppKit head is already proven by the reference app, so core UI viability is not in question. Only two residual capabilities matter, and neither blocks the v1 lighthouse core; this gate informs post-v1 planning and developer ergonomics rather than v1 viability.

**Independent Test**: In the running app, attempt to bring a local file into a web page file input and confirm whether the file's contents are accessible; separately, edit a UI element while the app runs and confirm whether the change appears without a full rebuild.

**Acceptance Scenarios**:

1. **Given** the running app, **When** the developer brings a local file into a page file input, **Then** the result (file contents accessible, or blocked by the web view's sandbox) is observed and recorded.
2. **Given** the running app, **When** the developer edits a UI element, **Then** whether the change is reflected via hot reload (versus requiring a full rebuild) is observed and recorded.
3. **Given** file intake is blocked, **When** the developer records the finding, **Then** the need for a native file-intake shim for post-v1 features is flagged.

---

### User Story 4 - Vertical slice and server capability check (M0d) (Priority: P1)

As the FoundryForge developer, I need an end-to-end vertical slice — an AppKit + Blazor Hybrid app that initializes Foundry Local in-process, lists the catalog, downloads and loads a small model, and streams one reply into a UI component — plus confirmation of whether the exposed local server honors tool-calling and structured-output requests, so that the full intended architecture is proven and downstream v1 scope is settled.

**Why this priority**: This is the gate that proves the actual product architecture (UI + in-process Foundry Local) works together, not just in isolation. Its server-capability check determines whether tool-calling and structured output stay in v1 scope or are descoped, so it directly bounds the v1 plan.

**Independent Test**: Launch the slice app on an Apple Silicon Mac, observe the catalog populate, watch a small model download and load, and see a streamed reply appear token-by-token in a UI component; separately, send tool-calling and structured-output requests to the exposed local server and record whether they are honored.

**Acceptance Scenarios**:

1. **Given** the vertical slice app, **When** the developer launches it on an Apple Silicon Mac, **Then** Foundry Local initializes in-process and the available model catalog is displayed.
2. **Given** the catalog is displayed, **When** the developer selects the designated small test model, **Then** the model downloads, loads, and a single prompt produces a reply that streams incrementally into a UI component.
3. **Given** a loaded model exposed via the local server, **When** the developer sends a tool-calling request and a structured-output request to the server, **Then** whether each is honored is recorded as a go/no-go input for the v1 tool-calling and structured-output scope.

---

### Edge Cases

- **A gate fails**: forward progress stops at that gate; a go/no-go decision with its evidence is recorded before any later gate or milestone begins. The cheapest possible failure point is preferred (toolchain before native load before UI before full slice).
- **net11 preview instability (M0a)**: fall back to the recorded net10 reference set, or record an explicit decision to proceed on net11 with rationale.
- **Library-validation failure on in-process load (M0b)**: apply the documented entitlement plus nested-dylib re-signing remedy; if still failing, declare no-go with evidence.
- **Web-view file intake blocked (M0c)**: record the limitation and flag the post-v1 native file-intake shim; this does not fail M0.
- **Server rejects tool-calling or structured output (M0d)**: record as a scope-bounding input; the corresponding v1 capability is descoped rather than faked.
- **First run with no network**: model download requires network on first run; the gate accounts for this and verifies offline operation only after the model is cached.
- **Inconclusive evidence**: a gate is not passed on "it built" alone; an end-to-end check on real hardware is required before a gate is marked green.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: M0 MUST be evaluated as four sequential sub-gates (M0a toolchain pin, M0b native-load spike, M0c capability probe, M0d vertical slice), each a go/no-go checkpoint completed before the next begins.
- **FR-002**: No application/product code beyond the minimal proof artifacts for each gate MUST be built until M0d passes.
- **FR-003**: M0a MUST verify that an empty AppKit + Blazor Hybrid app builds from a clean checkout and launches a rendering window on an Apple Silicon Mac using only the pinned version set.
- **FR-004**: The pinned version set used for M0 MUST be recorded in the known-good versions reference, with the net10 reference set documented as the fallback.
- **FR-005**: M0b MUST be performed in a minimal console head with no MAUI or Blazor dependencies.
- **FR-006**: M0b MUST confirm that the complete Foundry Local native dylib chain (core library plus its ONNX Runtime, ONNX Runtime GenAI, and Dawn dependencies) is bundled into the app package and that every inter-library reference resolves within the bundle.
- **FR-007**: M0b MUST confirm an in-process Foundry Local initialization and at least one successful inference, or declare the gate no-go with evidence.
- **FR-008**: If in-process load fails library validation, M0b MUST apply and record the entitlement plus nested-dylib re-signing remedy, or declare no-go.
- **FR-009**: M0c MUST observe and record whether local file intake into a web page input works and whether hot reload works, flagging any need for a post-v1 native file-intake shim.
- **FR-010**: M0d MUST demonstrate an end-to-end slice: in-process initialization, catalog listing, download and load of the designated small test model, and one reply streamed incrementally into a UI component.
- **FR-011**: M0d MUST record whether the exposed local server honors tool-calling and structured-output requests, as the go/no-go input for keeping those capabilities in v1 scope.
- **FR-012**: Each gate MUST be closed by an end-to-end check on a real Apple Silicon Mac, not by build success alone, and the milestone-closing note MUST end with a `Verified:` line naming the checks that ran.
- **FR-013**: Every go/no-go decision and any version-set or scope decision arising from M0 MUST be recorded in the project decision log.
- **FR-014**: Capability findings MUST be reported honestly: unsupported capabilities are surfaced as limits, never worked around with fake UI or dead toggles.
- **FR-015**: Any workaround applied to an upstream gap during M0 MUST be recorded with a tracking link in the known-issues reference and removed when the upstream fix lands.

### Key Entities *(include if feature involves data)*

- **Gate (M0a–M0d)**: a single feasibility checkpoint with a status (pending, passed, no-go), the evidence captured, and the decision recorded.
- **Pinned version set**: the specific package versions and SDK band proven during M0, with the net10 fallback reference noted.
- **Native dylib chain**: the Foundry Local core library plus its runtime dependencies that must bundle and resolve together.
- **Test model**: the designated small model used to prove download, load, and streamed inference.
- **Go/no-go decision record**: the recorded outcome and rationale for each gate, including any scope or version-set changes it forces.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An empty AppKit + Blazor Hybrid app on the pinned version set builds from a clean checkout and launches a rendering window on an Apple Silicon Mac (M0a green).
- **SC-002**: In a console head, the complete Foundry Local native dylib chain is bundled, every inter-library reference resolves, and one in-process inference returns a valid completion without a native crash or library-validation failure (M0b green).
- **SC-003**: The web-view file-intake and hot-reload findings are recorded, and any post-v1 file-intake shim need is explicitly flagged (M0c complete).
- **SC-004**: An end-to-end vertical slice initializes Foundry Local in-process, lists the catalog, downloads and loads the designated small test model, and streams at least one reply incrementally into a UI component on real hardware (M0d green).
- **SC-005**: A recorded determination exists for whether the exposed local server honors tool-calling and structured output, with the matching v1 scope decision made.
- **SC-006**: Each passed gate has a recorded `Verified:` line from a real-hardware end-to-end check, and every go/no-go decision is captured in the decision log.
- **SC-007**: 100% of M0 workarounds applied to upstream gaps have a tracking link recorded in the known-issues reference.

## Assumptions

- The target environment is macOS on Apple Silicon only; no other platforms are in scope for M0 or v1.
- The pinned net11 AppKit + Blazor Hybrid preview set is the primary track, with the proven net10 reference set as the documented fallback per the project plan.
- The reference app (a working MAUI + Blazor Hybrid + macOS AppKit app on the same package family) validates the overall stack and the native-bundling pattern that M0b adapts.
- A small, fast model is acceptable as the M0d test model; full model-catalog coverage is not required to pass the gate.
- First-run model download requires network connectivity; offline operation is expected only after a model is cached.
- M0 is human-in-the-loop and decision-oriented; it produces proof artifacts and recorded decisions, not user-facing product features.
- The canonical reference for M0 scope and sequencing is the project plan (`docs/PLAN.md`) until ratified into this spec; binding guardrails live in the project's agents, known-issues, and known-good-versions references.
