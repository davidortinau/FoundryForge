---
description: "Task list for M0 Feasibility Gate"
---

# Tasks: M0 Feasibility Gate

**Input**: Design documents from `specs/001-m0-feasibility-gate/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: No automated test tasks. M0 verification is **manual end-to-end on a real Apple Silicon Mac** via MAUI DevFlow (plus `otool`/`find`/binlog/`curl` evidence). The xUnit pure-logic seam is an M1 deliverable, not M0. This matches the spec (no TDD requested) and the constitution's "build success is a prerequisite, not verification" rule.

## ⚠️ Critical: gates are SEQUENTIAL, not parallel

Unlike a typical multi-story feature, the four user stories here are **go/no-go gates that must run in order**: M0a → M0b → M0c → M0d. A blocking gate at `no-go` halts everything after it. **User stories CANNOT be parallelized across team members.** `[P]` markers below apply only to independent tasks *within* a gate. See [`contracts/`](./contracts/) for each gate's exit criteria.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1=M0a, US2=M0b, US3=M0c, US4=M0d
- Paths follow plan.md: disposable spike heads under `spikes/`, shared target/entitlements at repo root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Skeleton for the disposable spike heads and the pinned-version + evidence conventions. No product `src/` (FR-002).

- [ ] T001 Create `spikes/` directory structure (`spikes/m0a-baseline-app/`, `spikes/m0b-fl-console/`, `spikes/m0d-vertical-slice/`) with a `spikes/README.md` stating these are throwaway M0 proof heads, not product code
- [ ] T002 [P] Create `Directory.Packages.props` at repo root with concrete, **non-floating** version entries (placeholders to be pinned by M0a/M0b); enable `ManagePackageVersionsCentrally`
- [ ] T003 [P] Add an "M0 evidence + decisions" convention note to `.squad/decisions.md` (each gate records go/no-go, rationale, and a `Verified:` line per `data-model.md`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Stage the toolchain every gate depends on.

**⚠️ CRITICAL**: No gate can begin until this phase is complete.

- [ ] T004 Stage the .NET 11 Preview MAUI CI packages and pinned `global.json` / `NuGet.config` / .csproj props using the `macos-maui-dogfood` skill (net11-primary track); confirm `dotnet --info` resolves the pinned SDK band on an Apple Silicon Mac

**Checkpoint**: Pinned net11 toolchain is staged and resolvable. M0a may begin.

---

## Phase 3: User Story 1 - Toolchain & stack baseline (M0a) (Priority: P1) 🎯 MVP

**Goal**: Prove the pinned `net11.0-macos` AppKit + Blazor Hybrid set builds and launches an empty app on real hardware.

**Independent Test**: Build + run the empty baseline app via DevFlow; confirm a native window opens and a trivial Blazor page renders. No Foundry Local.

**Exit contract**: [`contracts/gate-m0a-exit.md`](./contracts/gate-m0a-exit.md)

- [ ] T005 [US1] Create an empty Sherpa-shaped AppKit + BlazorWebView app in `spikes/m0a-baseline-app/` referencing the pinned `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` + matched `Microsoft.Maui.Controls` + `Microsoft.AspNetCore.Components.WebView.Maui`, with a trivial Razor page
- [ ] T006 [US1] Build from a clean checkout and confirm only pinned versions resolve (no floating ranges, no implicit "latest"): `dotnet build spikes/m0a-baseline-app`
- [ ] T007 [US1] Run on an Apple Silicon Mac and verify a window opens + the Blazor page renders: `dotnet build spikes/m0a-baseline-app -t:Run` then `maui devflow wait`
- [ ] T008 [US1] Record the exact proven version set in `KNOWN-GOOD-VERSIONS.md` with `track: net11-primary` (or, on fallback, the net10 Sherpa set with `track: net10-fallback`)
- [ ] T009 [US1] Record the M0a go/no-go decision + `Verified:` line in `.squad/decisions.md` (on net11 instability, record the net10 fallback decision and rationale)

**Checkpoint**: M0a `passed`. Toolchain is proven. M0b may begin.

---

## Phase 4: User Story 2 - Foundry Local native-load spike (M0b) (Priority: P1) 🔑 TRUE GO/NO-GO

**Goal**: Prove the full Foundry Local dylib chain bundles into the `.app`, resolves its `@rpath`s, loads in-process, and runs one inference — in a console head with no UI.

**Independent Test**: In `spikes/m0b-fl-console/`, build, confirm the chain bundled and resolves (`find` + `otool -L`), then `CreateAsync` + one inference succeeds.

**Exit contract**: [`contracts/gate-m0b-exit.md`](./contracts/gate-m0b-exit.md) · **Depends on**: M0a `passed`

- [ ] T010 [US2] Create a minimal macOS **console** head in `spikes/m0b-fl-console/` (`dotnet new macos`-shaped; **no MAUI, no Blazor**) referencing the Foundry Local SDK
- [ ] T011 [US2] Confirm Foundry Local package identity (`sdk` vs `sdk_v2`) by reading its source/release notes; pin the exact version in `KNOWN-GOOD-VERSIONS.md` and `Directory.Packages.props`
- [ ] T012 [P] [US2] Author `build/BundleFoundryLocalNative.targets` adapting Sherpa's `_BundleNativeForMacOS` to copy the FL chain (`libfoundry_local` + ONNX Runtime + ONNX Runtime GenAI + Dawn) into `Contents/MonoBundle/runtimes/osx-arm64/native/`
- [ ] T013 [P] [US2] Author `Entitlements.Debug.plist` at repo root (`EnableHardenedRuntime`; `network.server`, `cs.allow-jit`, `app-sandbox=false`, `get-task-allow`)
- [ ] T014 [US2] Build with a binlog (`dotnet build spikes/m0b-fl-console -bl:m0b.binlog`); verify the whole chain is bundled (`find ... -name '*.dylib'`) and every `@rpath` resolves (`otool -L` each); use `mcp-binlog-tool` on `m0b.binlog` to confirm the copy target fired
- [ ] T015 [US2] Run `CreateAsync` + one inference from the console head; confirm no `DllNotFoundException` / library-validation crash
- [ ] T016 [US2] If library validation blocked load: add `com.apple.security.cs.disable-library-validation` to `Entitlements.Debug.plist`, re-sign nested dylibs, and record the required entitlement (+ a `KNOWN-ISSUES.md` tracking link if it is an upstream workaround)
- [ ] T017 [US2] Record the M0b go/no-go decision + `Verified:` line in `.squad/decisions.md`; **on no-go, halt** and file a precise Foundry Local issue with a minimal repro and cited source line (route: Maanav Dalal / Meng Tang)

**Checkpoint**: M0b `passed` (or project halted with evidence). M0c may begin.

---

## Phase 5: User Story 3 - BlazorWebView capability probe (M0c) (Priority: P2)

**Goal**: Record whether web-view file intake and Hot Reload work on the AppKit head. Informational — does **not** halt M0.

**Independent Test**: In the running app, try a local file into `<input type=file>` and edit a UI element; record both outcomes.

**Exit contract**: [`contracts/gate-m0c-exit.md`](./contracts/gate-m0c-exit.md) · **Depends on**: M0b `passed`

- [ ] T018 [P] [US3] Add an `<input type=file>` to the baseline/slice app, attempt to read a dragged-in local file, and record accessible vs sandbox-blocked in `.squad/decisions.md`
- [ ] T019 [P] [US3] Edit a UI element while the app runs and record whether Hot Reload reflects it without a full rebuild
- [ ] T020 [US3] If file intake is blocked, flag the post-v1 **native file-intake shim** need in `.squad/decisions.md`; record a `Verified:` line for the probe run

**Checkpoint**: M0c `complete` (findings recorded). M0d may begin.

---

## Phase 6: User Story 4 - Vertical slice + server capability check (M0d) (Priority: P1)

**Goal**: Prove the real architecture end-to-end (in-process catalog → load → streamed reply) and record whether the exposed server honors `tools` + `response_format`.

**Independent Test**: Launch the slice; observe catalog → `qwen2.5-0.5b` download+load → streamed reply in a Razor component; `curl` the server for tool-calling + structured output.

**Exit contract**: [`contracts/gate-m0d-exit.md`](./contracts/gate-m0d-exit.md) · **Depends on**: M0a + M0b `passed`, M0c `complete`

- [ ] T021 [US4] Create the slice app in `spikes/m0d-vertical-slice/` (AppKit + BlazorWebView) with a thin in-process `IChatClient` adapter over the FL SDK (mirrors the intended M1 architecture; **no loopback socket** for the app's own chat)
- [ ] T022 [US4] Apply `build/BundleFoundryLocalNative.targets` + `Entitlements.Debug.plist` (proven in M0b) to the slice project
- [ ] T023 [US4] Implement: in-process `CreateAsync` (async-ready, **no `.Result`/`.Wait()`**) → list catalog (`ICatalog`) → download+load `qwen2.5-0.5b` → stream one reply incrementally into a Razor component
- [ ] T024 [US4] Verify end-to-end on an Apple Silicon Mac via DevFlow (`dotnet build spikes/m0d-vertical-slice -t:Run` → `maui devflow wait`): catalog populates, model downloads+loads, reply renders token-by-token
- [ ] T025 [US4] Start the exposed local server and probe it externally: `curl` a tool-calling request and a `response_format` request; record each as `yes`/`no`/`partial` in `.squad/decisions.md`
- [ ] T026 [US4] Record the v1 scope decision for tool-calling and structured output (keep or descope) honestly — no fake UI/toggle for an unsupported capability (capability-honesty principle)
- [ ] T027 [US4] Record the M0d go/no-go decision + `Verified:` line in `.squad/decisions.md`

**Checkpoint**: M0d `passed`. All four gates complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Finalize the record, gate the push, and prepare the handoff to M1.

- [ ] T028 [P] Finalize `KNOWN-GOOD-VERSIONS.md` (all `TBD` rows resolved to concrete pins) and `KNOWN-ISSUES.md` (every M0 workaround has a tracking link)
- [ ] T029 [P] Append a structured `DEC-00x — M0 feasibility: GO` entry to `.squad/decisions.md` summarizing all four gate outcomes + the v1 tool/structured-output scope decision
- [ ] T030 Run the [`quickstart.md`](./quickstart.md) validation top-to-bottom on a clean checkout to confirm the recorded evidence reproduces
- [ ] T031 Decide `spikes/` disposition (archive vs delete) and record it, so M1 scaffolds the real solution from a clean tree rather than promoting throwaway heads silently
- [ ] T032 PR Quality review (Squad: Spunkmeyer) enforcing reviewer independence — the author of a gate spike does not self-approve its go/no-go; run `/review` before any push

---

## Dependencies & Execution Order

### Phase / gate dependencies (STRICTLY SEQUENTIAL)

- **Setup (Phase 1)** → no deps, start immediately
- **Foundational (Phase 2: T004)** → depends on Setup; **blocks all gates**
- **M0a (US1)** → depends on T004
- **M0b (US2)** → depends on **M0a `passed`** (its true go/no-go; a no-go halts M0c/M0d/M1)
- **M0c (US3)** → depends on **M0b `passed`** (informational; never halts)
- **M0d (US4)** → depends on **M0a + M0b `passed`** and M0c `complete`
- **Polish (Phase 7)** → depends on M0d `passed`

> There is **no cross-gate parallelism**. This is the intended design of a staged feasibility gate (fail cheap, in order), and is a deliberate deviation from the template's "stories run in parallel" default.

### Within-gate dependencies

- M0a: T005 → T006 → T007 → T008 → T009
- M0b: T010 → T011 → (T012 ∥ T013) → T014 → T015 → T016 (conditional) → T017
- M0c: (T018 ∥ T019) → T020
- M0d: T021 → T022 → T023 → T024 → T025 → T026 → T027

### Parallel opportunities (the only ones)

- Setup: T002 ∥ T003
- M0b: T012 (bundling target) ∥ T013 (entitlements plist) — different files
- M0c: T018 (file intake) ∥ T019 (Hot Reload) — independent observations
- Polish: T028 ∥ T029

---

## Suggested Squad ownership (coordinator routes tasks.md)

- **Vasquez (Native & Packaging)**: T001, T004, T012, T013, T014, T016, T022 — toolchain, bundling target, entitlements, dylib verification
- **Bishop (Foundry Local Integration)**: T010, T011, T015, T021, T023, T025 — FL SDK, `CreateAsync`, in-process adapter, server probe
- **Hicks (Blazor UI)**: T005, T007, T018, T019, T024 — Razor pages, BlazorWebView, capability probes, streamed render
- **Ripley (Lead/Strategy)**: T009, T017, T026, T027, T029, T031 — go/no-go decisions, scope calls, decision log
- **Drake (Test & CI)**: T006, T030 — clean-checkout build discipline, quickstart reproduction
- **Spunkmeyer (PR Quality)**: T032 — reviewer independence + `/review` gate
- **Scribe (Session Memory)**: appends every decision to `.squad/decisions.md`

---

## Implementation Strategy

### The gate IS the MVP

M0 has no shippable feature; its "MVP" is **M0b passing** — the proof the architecture is viable. Sequence: Setup → Foundational → M0a → **M0b (stop and validate; a no-go ends the project here)** → M0c → M0d. Each gate fails cheap and is recorded before the next begins. Do not build product `src/` until M0d is `passed`.

### Hard stops

- After **M0b**: if no-go, halt — file the FL issue, do not proceed to M0c/M0d/M1.
- After **M0d**: if no-go, halt — record evidence, do not start M1.
- A server-capability `no` in M0d is **not** a halt — it descopes a v1 capability.

---

## Notes

- `[P]` = different files, no incomplete dependency; used sparingly here by design.
- Every gate closes with a real Apple-Silicon DevFlow end-to-end check and a `Verified:` line — build success alone never passes a gate.
- Every upstream workaround gets a `KNOWN-ISSUES.md` tracking link and is removed when the fix lands.
- Capability honesty: unsupported FL features are surfaced as limits, never faked.
