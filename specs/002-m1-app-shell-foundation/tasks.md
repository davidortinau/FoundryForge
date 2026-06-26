---
description: "Task list for M1 — App Shell + Foundry Local Service Layer + DI + Test/CI Seam"
---

# Tasks: M1 — App Shell + Foundry Local Service Layer + DI + Test/CI Seam

**Input**: Design documents from `specs/002-m1-app-shell-foundation/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R11), data-model.md, contracts/ (6 service-interface contracts)

**Tests**: INCLUDED. The spec mandates a test/CI seam (US3 is a **P1** user story, not optional) and pure-logic seam coverage over the FL-free `FoundryStudio.Core` (settings, catalog filtering, RAM-fit heuristic, concurrency gate). "Tests" here are framed around **verifiable service behavior** — ready-state reached without a `.Result`/`.Wait()` deadlock, the concurrency gate draining/rejecting/serializing correctly, settings persisting without silent wipe, and CI building clean on pinned versions — per the spec (foundation milestone, no end-user screens).

**Organization**: Tasks are grouped by user story (US1–US5) so each is independently implementable and testable, in priority order (P1 → P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1=ready-gated lifecycle, US2=concurrency gate, US3=test+CI seam, US4=catalog service + chat adapter, US5=settings store
- Paths follow plan.md's **four-project** layout: `src/FoundryStudio.Core` (net10.0, FL-free), `src/FoundryStudio.Foundry` (net10.0, FL-bound), `src/FoundryStudio.App` (net10.0-macos, AppKit + Blazor Hybrid head), `tests/FoundryStudio.Tests` (net10.0, references **Core only**)

## Prerequisite risk (non-blocking)

The **net11 toolchain pin** (the separate open chore referred to as **T004** in PLAN/spec) is **out of M1 scope**. M1 builds on the proven `net10.0-macos` baseline and does **not** block on it (Assumptions; FR-019). Tasks below flag where it is relevant but planning does not wait on it.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the real, non-throwaway solution skeleton, reuse the M0-proven pinned build assets as-is, and retire the disposable spikes (FR-001, R7, R11).

- [ ] T001 Create `FoundryStudio.sln` and the four projects with correct TFMs + project references: `src/FoundryStudio.Core/FoundryStudio.Core.csproj` (`net10.0`, **NO** Foundry Local SDK reference), `src/FoundryStudio.Foundry/FoundryStudio.Foundry.csproj` (`net10.0`, references Core + `Microsoft.AI.Foundry.Local` + `Microsoft.Extensions.AI`), `src/FoundryStudio.App/FoundryStudio.App.csproj` (`net10.0-macos`, `Microsoft.NET.Sdk.Razor`, references Core + Foundry), `tests/FoundryStudio.Tests/FoundryStudio.Tests.csproj` (`net10.0`, xUnit, references **Core ONLY**) (FR-001, R7)
- [ ] T002 [P] Reuse the pinned `Directory.Packages.props` at repo root as-is; confirm `ManagePackageVersionsCentrally=true`, that all four projects consume the pinned set with **no inline PackageReference versions**, and that the FL `sdk` line `1.2.3` + maui-labs AppKit `0.1.0-preview.8.26256.5` + MEAI `10.0.1` entries are present (source of truth `KNOWN-GOOD-VERSIONS.md`; FR-017)
- [ ] T003 [P] Import `build/BundleFoundryLocalNative.targets` in `src/FoundryStudio.App/FoundryStudio.App.csproj` **ONLY**, and wire `Entitlements.Debug.plist` via `CodesignEntitlements`; verify neither Core nor Tests imports the bundle target (keeps the test seam dylib-free) (R7, E3)
- [ ] T004 [P] Spikes disposition (R11/FR-001): delete/archive `spikes/m0a-baseline-app`, `spikes/m0b-fl-console`, `spikes/m0d-vertical-slice`; prune `spikes/README.md` to state they were throwaway and where their proven patterns now live in the real projects
- [ ] T005 [P] Create `src/FoundryStudio.App/_Imports.razor` with the **FULL** MAUI Blazor template using-set (`Microsoft.AspNetCore.Components`, `…Components.Web`, `…Components.Forms`, `…Components.Routing`, `Microsoft.JSInterop`, plus app namespaces) so `@onclick`/`@bind` compile as interactive (KI-006, FR-002)
- [ ] T006 [P] Record the net11 toolchain pin (the open chore referred to as **T004** in PLAN/spec) as a **prerequisite risk, non-blocking** entry in `KNOWN-ISSUES.md`; confirm M1 targets the proven `net10.0-macos` baseline (FR-019, Assumptions)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The FL-free Core contracts, DTOs, and the compilable app shell that **every** user story codes against. No story behavior yet.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T007 [P] Lifecycle + gate interfaces in `src/FoundryStudio.Core/Abstractions/`: `IFoundryLifecycle.cs` (+ `FoundryReadyState` enum; `GetManagerAsync` returns `Task<object>` so Core stays FL-free) and `IModelStateGate.cs` (+ `MutationPolicy` enum, `ModelBusyException`) (contracts: IFoundryLifecycle, IModelStateGate)
- [ ] T008 [P] Service interfaces in `src/FoundryStudio.Core/Abstractions/`: `IFoundryCatalogService.cs`, `IChatService.cs`, `ISettingsService.cs` (contracts: IFoundryCatalogService, IChatService, ISettingsService)
- [ ] T009 [P] Post-v1 interfaces in `src/FoundryStudio.Core/Abstractions/`: `IEmbeddingService.cs`, `ITranscriptionService.cs`, `ILocalServerService.cs` (each exposes `IsSupported`) (FR-013, contract: IPostV1Services)
- [ ] T010 [P] FL-free catalog DTO records in `src/FoundryStudio.Core/Models/`: `ModelInfo.cs`, `ModelVariant.cs` (+ `Device` enum `Cpu|Gpu|Npu`) (data-model.md)
- [ ] T011 [P] FL-free settings model in `src/FoundryStudio.Core/Models/`: `AppSettings.cs` (+ `AppTheme` enum `Light|Dark|Auto`, `SchemaVersion`) (data-model.md, FR-014)
- [ ] T012 [P] FL-free seam DTOs in `src/FoundryStudio.Core/Models/`: `CatalogFilter.cs` (criteria record: Device?/Task?/Provider?/SearchText?/CachedOnly) and `RamFitResult.cs` (+ `RamFit` enum `Comfortable|Tight|Unlikely`) (data-model.md)
- [ ] T013 App shell scaffold (empty, compilable, launches a bare window) in `src/FoundryStudio.App/`: `Program.cs` (NSApplication bootstrap from M0d), `App.cs`, `AppDelegate.cs`, `BlazorHostPage.cs`, `Info.plist`, `wwwroot/index.html` + `wwwroot/app.css` (BundleResource), `Components/App.razor` + `Components/Routes.razor` (FR-001)
- [ ] T014 `src/FoundryStudio.App/MauiProgram.cs` `CreateMauiApp` with `BlazorWebView` + a `RegisterFoundryStudioServices` DI seam (filled incrementally per story) + DevFlow `{Agent,Blazor}` registered **Debug-only** (R1, FR-019)
- [ ] T015 Buildability checkpoint: `dotnet build FoundryStudio.sln -c Debug` compiles all four projects clean on the pinned set before story work begins (precedes the US3 CI gate, not a substitute for it)

**Checkpoint**: Foundation ready — US1–US5 can proceed (P1 first; P2 stories can parallelize once Foundational is done).

---

## Phase 3: User Story 1 - Ready-gated Foundry Local lifecycle manager + DI (Priority: P1) 🎯 MVP

**Goal**: The real app starts, wires a single Foundry Local lifecycle service through DI, runs heavy native init off the dispatcher behind a ready-gate every consumer awaits, and reaches a well-defined "ready" state without freezing or deadlocking the UI (KI-005).

**Independent Test**: Launch the app skeleton on Apple Silicon; observe an "initializing" state that blocks the chat surface, then a transition to "ready"; confirm via behavior that init offloads to a background thread, all awaiters receive the one shared instance, and no path blocks the init task with a synchronous wait.

- [ ] T016 [US1] `src/FoundryStudio.Foundry/FoundryLifecycle.cs`: the single `FoundryLocalManager` wrapper implementing `IFoundryLifecycle` + `IAsyncDisposable`; `Lazy<Task<FoundryLocalManager>>` factory = `Task.Run(InitializeAsync)` (off the BlazorWebView dispatcher), `ReadyAsync(ct)`, strongly-typed `GetManagerAsync(ct)` overload, `State`; **hard rule: no `.Result`/`.Wait()`** on the init task (KI-005; FR-003/004/005/006; contract IFoundryLifecycle)
- [ ] T017 [US1] Register `IFoundryLifecycle → FoundryLifecycle` as a **DI singleton** in `src/FoundryStudio.App/MauiProgram.cs`; guarantee no code path constructs a second manager (FR-003, SC-002)
- [ ] T018 [US1] App-level "initializing" guard in `src/FoundryStudio.App/Components/Pages/`: `Initializing.razor` + `Ready.razor`; wire `Components/Routes.razor` to block not-yet-ready surfaces (including the chat surface) until `ReadyAsync` is satisfied; components `await ReadyAsync()` in `OnInitializedAsync` then `await InvokeAsync(StateHasChanged)` (FR-004/005, SC-001)
- [ ] T019 [US1] Honest failed/slow-init handling in `FoundryLifecycle.cs` + `Initializing.razor`: surface a diagnosed cause, keep surfaces blocked, and ensure the gate is **never** satisfied by a failed init (`State == Failed`/`Initializing`) (edge case, SC-001)
- [ ] T020 [US1] Dispose the manager cleanly on app exit via `FoundryLifecycle.DisposeAsync` wired into shutdown, **without** a synchronous block on an in-flight init task (FR-007, SC-003, edge case)
- [ ] T021 [P] [US1] No-blocking guard test in `tests/FoundryStudio.Tests/NoBlockingInitGuardTests.cs`: scan repo source for `.Result`/`.Wait()` on the init task and assert **zero** occurrences (codifies KI-005; FR-006, SC-003)

**Checkpoint**: App launches → "initializing" → "ready" with no deadlock; one shared manager; zero synchronous blocking. **This is the MVP.**

---

## Phase 4: User Story 2 - Singleton load/unload concurrency gate (Priority: P1)

**Goal**: A single gate that drains-or-rejects in-flight generations before mutating model state, serializes concurrent mutations, and never loads/unloads a model while a generation streams on **that** model — pure logic, testable with no UI and no dylib.

**Independent Test**: Through the service layer alone (no UI), start a simulated in-flight generation, then request load/unload of that model; confirm the gate drains or rejects (typed `ModelBusyException`), never mutates mid-stream, serializes concurrent mutations, and isolates per model.

- [ ] T022 [P] [US2] `src/FoundryStudio.Core/Concurrency/ModelStateGate.cs` implementing `IModelStateGate`: per-model `SemaphoreSlim(1,1)` mutation lock + active-generation count; `BeginGenerationAsync(modelId)` → `IAsyncDisposable` lease; `MutateAsync` with `Drain` (await active→0, bounded) / `Reject` (throw `ModelBusyException`); per-model isolation; pure/FL-free (FR-008/009/010, R2; contract IModelStateGate)
- [ ] T023 [US2] Register `IModelStateGate → ModelStateGate` as a **DI singleton** in `src/FoundryStudio.App/MauiProgram.cs` (one gate backs the one manager and the future exposed server) (FR-009, Constitution V)
- [ ] T024 [P] [US2] `tests/FoundryStudio.Tests/ModelStateGateTests.cs`: drains (waits for lease disposal), rejects (`ModelBusyException`), concurrent-mutation serialization (0 observed concurrent mutations), per-model isolation (mutate B while A leased) — runs with **no native dylib** (SC-004, SC-008)

**Checkpoint**: Gate verified through the service layer with no UI/dylib; rejections are honest/typed.

---

## Phase 5: User Story 3 - Test project + CI seam from day one (Priority: P1)

**Goal**: A unit-test project (`tests/FoundryStudio.Tests`, references Core only — scaffolded in T001) plus one CI job that restores+builds+tests the whole solution on a clean checkout against the pinned versions, failing if any dependency floats off the pinned set.

**Independent Test**: On a clean checkout, run CI and confirm it restores/builds the full solution on **only** the pinned versions; run the test project and confirm the pure-logic seam tests pass with no native Foundry Local dylib present.

- [ ] T025 [US3] Enable `RestorePackagesWithLockFile=true` and commit lock files so a locked-mode restore **fails** if any dependency resolves off the pinned `Directory.Packages.props` set (FR-017, SC-009)
- [ ] T026 [US3] `.github/workflows/ci.yml`: one macOS Apple-Silicon job — clean checkout → `dotnet restore --locked-mode` → `dotnet build FoundryStudio.sln -c Debug` → `dotnet test tests/FoundryStudio.Tests -c Debug`, all on the pinned set; the job **fails** on float, build break, or seam-test failure (FR-017, SC-009, R10)
- [ ] T027 [US3] Verify `tests/FoundryStudio.Tests` references **Core only** (no FL/MEAI/dylib transitive) and that the pure-logic seam tests execute green in an environment with **no native Foundry Local dylib present** (FR-016, SC-008)
- [ ] T028 [US3] Wire the FR-006/SC-003 no-`.Result`/`.Wait()` guard (T021) into the CI job so a KI-005 regression fails the build (SC-012)

**Checkpoint**: Clean-checkout CI green on pinned versions; seam tests pass dylib-free; the pinning + KI-005 guardrails are enforced from day one.

---

## Phase 6: User Story 4 - Catalog service + in-process chat-client adapter (Priority: P2)

**Goal**: A catalog service wrapping FL catalog/model ops behind a stable FL-free interface (load/unload routed through the gate) plus a thin in-process `IChatClient` adapter over the FL SDK with **no loopback socket**; the post-v1 stubs that keep the DI graph stable for M5/M6.

**Independent Test**: Through the service layer (no UI), exercise catalog ops against the ready-gated manager and confirm each maps to the right FL op and routes load/unload through the gate; confirm the chat adapter streams in-process with zero loopback sockets and composes MEAI middleware.

- [ ] T029 [P] [US4] `src/FoundryStudio.Core/Catalog/CatalogFilter.cs` pure predicates over `ModelInfo`/`ModelVariant` (device/task/provider/search/cachedOnly; null criteria = match all; cached vs loaded partition helpers) (FR-016, R6)
- [ ] T030 [P] [US4] `src/FoundryStudio.Core/Catalog/RamFitHeuristic.cs` pure `Evaluate(sizeGb, freeRamGb)` → `RamFitResult` (size vs **free** RAM with a wide margin, long-context KV-cache caveat, never a confident green verdict) (FR-016, R5)
- [ ] T031 [P] [US4] `tests/FoundryStudio.Tests/CatalogFilterTests.cs` + `tests/FoundryStudio.Tests/RamFitHeuristicTests.cs` — pure seams, no dylib (SC-008)
- [ ] T032 [US4] `src/FoundryStudio.Foundry/FoundryCatalogService.cs` implementing `IFoundryCatalogService`: wraps `ICatalog`/`IModel`, maps results into `ModelInfo`/`ModelVariant`, implements Browse/GetModel/GetVariants/ListCached/ListLoaded/Download; `LoadAsync`/`UnloadAsync` route through `IModelStateGate.MutateAsync`; `DeleteFromCacheAsync` requires `userConfirmed` (protected user data) (FR-011, SC-005, Constitution IV; contract IFoundryCatalogService)
- [ ] T033 [US4] `src/FoundryStudio.Foundry/FoundryChatClient.cs` in-process `IChatClient` adapter (Microsoft.Extensions.AI) backed by `IModel.GetChatClientAsync()` → `CompleteChatStreamingAsync`; maps MEAI `ChatMessage` ↔ FL request message; acquires a `ModelStateGate` generation lease per stream; **no `127.0.0.1` loopback socket** (FR-012, SC-006, R3; contract IChatService)
- [ ] T034 [US4] `src/FoundryStudio.Foundry/ChatService.cs` implementing `IChatService` over the adapter, shaped so MEAI middleware composes (`AsBuilder().UseFunctionInvocation().UseOpenTelemetry()` seam reserved for M4); structured output treated as **best-effort only** — no "guaranteed JSON" surface (FR-012/018, SC-010, E4)
- [ ] T035 [P] [US4] Post-v1 honest stubs in `src/FoundryStudio.Foundry/PostV1/`: `StubEmbeddingService.cs`, `StubTranscriptionService.cs`, `StubLocalServerService.cs` — `IsSupported == false`, operations throw `NotSupportedException("Not implemented in v1 …")` (never fake/empty) (FR-013, contract IPostV1Services)
- [ ] T036 [US4] Register catalog, chat, and post-v1 stub services in `src/FoundryStudio.App/MauiProgram.cs` DI so the dependency graph is stable for M2/M4/M5/M6 (FR-011/012/013)
- [ ] T037 [P] [US4] `tests/FoundryStudio.Tests/PostV1StubTests.cs`: `IsSupported == false` and operations throw `NotSupportedException` rather than returning fake/empty data (FR-013, SC-010)

**Checkpoint**: Catalog + chat seams exist behind stable interfaces; load/unload routes through the gate; in-process chat opens 0 loopback sockets; post-v1 stubs are honest.

---

## Phase 7: User Story 5 - App settings / persistence store (Priority: P2)

**Goal**: Settings (model cache directory, default model, theme) persisted across launches, user-editable and auditable, never wiped or destructively overwritten without explicit per-action consent.

**Independent Test**: Through the settings store (no UI), write cache dir/default model/theme; restart the process; read back unchanged; confirm a destructive reset without consent is a no-op and that no path silently wipes settings; inspect the on-disk JSON for human-auditability.

- [ ] T038 [P] [US5] `src/FoundryStudio.Core/Settings/SettingsDocument.cs` pure logic: documented defaults (incl. default model cache directory), JSON (de)serialization, merge-missing-with-defaults, and the `RequireConsent` guard preventing destructive overwrite/clear without an explicit consent flag (FR-014/015, R4; contract ISettingsService)
- [ ] T039 [P] [US5] `tests/FoundryStudio.Tests/SettingsDocumentTests.cs`: defaults when unset; set→read round-trip fidelity; corrupt/missing/empty input → defaults + original file preserved (`.bak`); reset-without-consent is a no-op; reset-with-consent proceeds — all dylib-free (SC-007, SC-008)
- [ ] T040 [US5] `src/FoundryStudio.Foundry/PreferencesSettingsService.cs` implementing `ISettingsService`: persist a human-readable JSON document in app data anchored via MAUI Essentials `Preferences`; `GetAsync`/`UpdateAsync`/`ResetAsync(userConfirmed)`; non-destructive recovery (keep `.bak` of an unparseable file) (FR-014/015, R4)
- [ ] T041 [US5] Register `ISettingsService → PreferencesSettingsService` as a **DI singleton** in `src/FoundryStudio.App/MauiProgram.cs` (FR-014)
- [ ] T042 [US5] Confirm the documented default `ModelCacheDirectory` platform path is returned when unset, and that the cache directory it references (multi-GB protected user data) is never wiped/overwritten without consent (FR-014/015, Constitution IV)

**Checkpoint**: Settings persist with 100% fidelity across restart; consent-gated; on-disk JSON is auditable/editable.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Capability honesty, KI codification, and the NON-NEGOTIABLE milestone-close verification.

- [ ] T043 [P] Capability-honesty audit (FR-018, SC-010): confirm **no** UI/toggle exists for any unsupported FL feature (server auth/LAN bind, GGUF import, `top_k`/`min_p`/`seed`, speculative decoding) and that structured output is represented best-effort only (no "guaranteed JSON")
- [ ] T044 [P] KI codification + workaround tracking (FR-020, SC-012): confirm KI-005 (off-dispatcher init, no `.Result`/`.Wait()`) and KI-006 (full `_Imports` using-set) are codified in the service layer and app scaffold respectively, and that any new M1 workaround has a `KNOWN-ISSUES.md` tracking entry
- [ ] T045 Run `quickstart.md` Scenarios 1–7 on Apple Silicon: build clean on pins; xUnit green with no dylib; launch → "initializing" → "ready" with no deadlock (DevFlow DOM-confirmed, KI-001/KI-004); concurrency-gate service smoke; in-process chat with 0 loopback sockets (`lsof`); settings persist; capability honesty (FR-021, SC-011)
- [ ] T046 Reviewer independence (FR-022, SC-011): ensure the change set is approved by **someone other than its author**; run `/review` before push
- [ ] T047 Write the M1 closing note ending with a `Verified:` line naming the checks that ran (clean-checkout CI green on pinned net10 set; xUnit seam tests green dylib-free; launch-to-ready no deadlock; gate drained/rejected/serialized; one in-process streamed reply with 0 loopback sockets) (FR-021, SC-011)
- [ ] T048 [P] Append the M1 decision summary + `Verified:` line to `.squad/decisions.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately. T001 must precede all others.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**.
- **User Stories (Phase 3–7)**: All depend on Foundational completion.
  - **P1 first**: US1, US2, US3 (US1 is the MVP and a prerequisite for the *live* halves of US4's chat/catalog smoke and for T045).
  - US2 and US3 are independent of US1 at the pure-logic level and can run in parallel with it.
  - **P2**: US4 and US5 can start once Foundational is done; they integrate with US1 (ready-gate) and US2 (concurrency gate) for their live paths but their pure seams + tests are independent.
- **Polish (Phase 8)**: Depends on all desired user stories being complete; T045–T047 are the milestone-close gate.

### User Story Dependencies

- **US1 (P1)**: Foundational only. The MVP spine.
- **US2 (P1)**: Foundational only. Pure-logic; fully independent of US1.
- **US3 (P1)**: Foundational + the test project scaffold (T001). CI (T026) green is strongest once US2/US4/US5 seam tests exist, but the gate itself stands alone.
- **US4 (P2)**: Foundational; routes load/unload through **US2**'s gate (T032 depends on T022) and its live chat/catalog smoke awaits **US1**'s ready-gate (T016). Pure seams (T029–T031) are independent.
- **US5 (P2)**: Foundational only. Fully independent (pure logic + Preferences binding).

### Within Each User Story

- Core pure-logic/seam before the FL-bound implementation that consumes it.
- Implementation before its DI registration.
- DI registration before the live (Apple-Silicon) smoke.
- Each story's tests can be written alongside or before its implementation (verify they fail first where applicable).

### Parallel Opportunities

- All Setup `[P]` tasks (T002–T006) after T001.
- All Foundational `[P]` interface/DTO tasks (T007–T012) together; T013/T014 follow.
- Once Foundational completes, **US1, US2, US3 (P1) and US4, US5 (P2)** can be staffed in parallel by different owners.
- Within US4: pure seams T029/T030 and tests T031, plus stubs T035 and stub tests T037, are `[P]`.
- Within US5: T038 (pure) and T039 (tests) are `[P]`.

---

## Parallel Example: Foundational interfaces & DTOs

```bash
# Launch all FL-free Core contracts + DTOs together (different files, no deps):
Task: "Lifecycle + gate interfaces in src/FoundryStudio.Core/Abstractions/ (T007)"
Task: "Service interfaces in src/FoundryStudio.Core/Abstractions/ (T008)"
Task: "Post-v1 interfaces in src/FoundryStudio.Core/Abstractions/ (T009)"
Task: "Catalog DTOs in src/FoundryStudio.Core/Models/ (T010)"
Task: "Settings model in src/FoundryStudio.Core/Models/ (T011)"
Task: "Seam DTOs in src/FoundryStudio.Core/Models/ (T012)"
```

## Parallel Example: P2 pure seams + tests (US4)

```bash
Task: "CatalogFilter pure predicates in src/FoundryStudio.Core/Catalog/CatalogFilter.cs (T029)"
Task: "RamFitHeuristic pure logic in src/FoundryStudio.Core/Catalog/RamFitHeuristic.cs (T030)"
Task: "CatalogFilterTests + RamFitHeuristicTests in tests/FoundryStudio.Tests/ (T031)"
Task: "Post-v1 honest stubs in src/FoundryStudio.Foundry/PostV1/ (T035)"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: US1 — app launches → "initializing" → "ready", one shared manager, zero synchronous blocking.
4. **STOP and VALIDATE**: launch on Apple Silicon; confirm ready-without-deadlock (DevFlow DOM).
5. This is the demonstrable MVP foundation.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 (ready-gate) → MVP launch-to-ready.
3. US2 (concurrency gate) + US3 (test/CI) → P1 guardrails green and enforced.
4. US4 (catalog + chat + stubs) and US5 (settings) → service seams M2–M6 build on.
5. Polish → capability-honesty audit, KI codification, and the `Verified:` close.

### Parallel Team Strategy

With multiple owners, after Foundational completes: P1 stories US1/US2/US3 and P2 stories US4/US5 proceed concurrently; integration points (US4 → US1 ready-gate + US2 gate) are the only cross-story dependencies.

---

## Notes

- `[P]` = different files, no dependency on an incomplete task.
- `[Story]` label maps each task to its user story for traceability; Setup/Foundational/Polish carry no story label by design.
- The Core/Foundry split is deliberate (R7): it makes "tests run with no native dylib" **structural**, not an assumption (FR-016, SC-008).
- Hard rules to keep green throughout: exactly **one** `FoundryLocalManager` singleton; **no `.Result`/`.Wait()`** on the init task; in-process chat with **no loopback socket**; pinned versions never floated; settings never wiped without consent.
- Build success is a **prerequisite, not verification** — M1 is done only when T045–T047 pass and the closing note ends with a `Verified:` line approved by someone other than the author.

---

## Suggested Squad ownership (coordinator routes tasks.md)

- **Ripley (Lead/Strategy)**: T001, T007, T008, T009, T047 — solution shape, the FL-free Core contracts, scope calls, milestone-close decision/`Verified:` note
- **Bishop (Foundry Local Integration)**: T016, T019, T020, T032, T033, T034, T035, T040 — `FoundryLifecycle` ready-gate, FL catalog/chat adapter, post-v1 stubs, settings persistence binding
- **Hicks (Blazor UI)**: T005, T013, T014, T017, T018 — full `_Imports`, app shell scaffold, `MauiProgram`/BlazorWebView + DI wiring, initializing/ready pages
- **Vasquez (Native & Packaging)**: T002, T003, T004, T006 — pinned `Directory.Packages.props`, bundle target + entitlements (App-only), spikes disposition, net11 prerequisite-risk note
- **Drake (Test & CI)**: T010, T011, T012, T015, T021, T022, T023, T024, T025, T026, T027, T028, T029, T030, T031, T037, T038, T039, T045 — Core DTOs/pure seams + concurrency gate, the test project + CI gate (clean-checkout, locked-mode, dylib-free), KI-005 guard, quickstart reproduction
- **Spunkmeyer (PR Quality)**: T046 — reviewer independence + `/review` gate (author never sole approver)
- **Cross-cutting (lead-coordinated)**: T036 (service DI registration), T041 (settings DI), T042 (cache-dir consent), T043 (capability-honesty audit), T044 (KI codification), T048 (decision log append)
- **Scribe (Session Memory)**: appends every decision + `Verified:` line to `.squad/decisions.md` (T048)
