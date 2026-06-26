---
description: "Task list for M5 — Local server toggle (the v1 'wow')"
---

# Tasks: M5 — Local server toggle (the v1 "wow")

**Input**: Design documents from `/specs/006-m5-server-toggle/`

**Prerequisites**: plan.md (Constitution PASS; concrete file layout — extend the 4 existing projects, no new projects/packages/store), spec.md (7 in-scope user stories 5×P1/1×P2/1×P3, FR-001–028, SC-001–011), research.md (StartAsync→StartWebServiceAsync+Urls mapping R1 / observable-request-activity R2 PINNED for hardware / documented route list R3 / IModelStateGate coordination R4 / IsSupported determination R5), data-model.md (ServerState, ServerStatus, ServerStateMachine, ServerEndpoints/ServerRoute, ServerLimitations, RequestActivityProjection), contracts/ (core-seams.md, service-surface.md, server-ui.dom.md), quickstart.md (Layer A dylib-free unit tests + Layer B Apple-Silicon DevFlow DOM e2e incl. **EXTERNAL curl** proof + connection-refused-after-Stop + concurrency + chat-unaffected).

**Tests**: M5 ships **dylib-free xUnit** in `tests/FoundryStudio.Tests` for **every** Core seam (`ServerStateMachine`, `ServerEndpoints`, `ServerLimitations`, `RequestActivityProjection`, and the gate-coordination/busy-mapping seam over a **fake** `IModelStateGate`). The single FL-bound `LocalServerService` (Foundry layer) is **not** unit-tested in the Core-only project — its behavior is proven by Layer B on real Apple Silicon, and its *coordination logic* is mirrored by `ServerGateCoordinationTests`. All UI behavior is verified via **DevFlow DOM inspection** on real Apple Silicon (KI-001 — the sanctioned autonomous evidence path; `ui screenshot` needs the window frontmost and is a light/dark supplement, never the gate), and the **defining proof** is an **external out-of-process `curl http://127.0.0.1:<port>/v1/chat/completions`** returning a real model response.

**Organization (matches how we implement)**: Core dylib-free seams + their unit tests land **first** (coordinator-authored, unit-testable in the Core-only test project) → then the single **Foundry-layer wiring** (`LocalServerService` over the shared `FoundryLocalManager`, coordinated by `IModelStateGate`; the only new FL-bound piece) + DI swap (coordinator/Bishop) → then the **Blazor "Forge Lit" server UI** grouped by user story (Hicks) → then **verification** (Layer A unit suites + Layer B DevFlow DOM e2e incl. external curl, connection-refused, concurrency, chat-unaffected; light/dark). Story labels (US1–US7) trace every task to its spec story.

## M5 Guardrails (apply to EVERY task)

- **Capability honesty (Constitution III/IV)** — the milestone's core: **0** port field/slider/dropdown (FL exposes no port parameter → show verbatim `Urls`); **0** auth/API-key controls and **0** LAN/`0.0.0.0`/remote-bind controls (FL lacks them → surface as plain limit **text**, never dead/enabled toggles, FR-007/020); the request log is shown **only** if FL exposes observable activity, otherwise **omitted with an honest note** and **zero** fabricated entries (FR-022/023); an **empty `Urls`** after a reported start is treated as a failed/incomplete start, **never** a fabricated endpoint (FR-006, R1); the copper pilot-light is lit **only** when truly `Running` (FR-013). Every value shown comes from the real service/SDK (FR-025).
- **Real bound URL only**: the endpoint/copy payload is always the **verbatim** `ILocalServerService.Urls` value (`ServerEndpoints.BaseUrl`/`CopyPayload`), never a hard-coded/assumed host:port; empty `Urls` ⇒ `null` ⇒ no copyable "live" URL (FR-008/009, SC-001/003).
- **Layering (Constitution V / DEC-004)**: `FoundryStudio.App` consumes **only** `ILocalServerService` + the Core seams (`ServerState`/`ServerStatus`, `ServerStateMachine`, `ServerEndpoints`, `ServerLimitations`, `RequestActivityProjection`) + the M1 lifecycle/gate — **never** `Microsoft.AI.Foundry.Local`/FL types (FR-024). All FL types stay inside `FoundryStudio.Foundry`; `LocalServerService` is the **only** new FL-bound class.
- **Single manager + concurrency (Constitution V)**: start/stop use the **one** `FoundryLocalManager` via `FoundryLifecycle.GetManagerTypedAsync()` — **never** a second manager (FR-003, SC-011); they `await ReadyAsync()` and serialize through the single `IModelStateGate.MutateAsync(..., MutationPolicy.Drain, ...)` so a server toggle never races an in-flight load/unload on shared native state (R4, FR-015); a conflict drains or surfaces an honest "busy, try again" mapped from `ModelBusyException` (FR-016).
- **KI-005 (NON-NEGOTIABLE)**: **no** `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` anywhere in the server path; start/stop are fully `await`ed off the BlazorWebView dispatcher and re-rendered via `await InvokeAsync(StateHasChanged)`, never blocking (FR-017). The `NoBlockingInitGuard` test stays green.
- **Independence**: the server is **external-tools only** — in-app chat (M4) does **not** route through it and behaves identically whether the server is stopped, starting, or running (FR-002/021, SC-005).
- **Seam purity**: the Core seams stay FL-free and dylib-free so the CI seam gate stays green. **No new package references, no new project, no persistent store** — the Core seams are plain managed code; the route list is static data (FR-018: next launch starts in the honest **stopped** state).
- **Accessibility (FR-014/026)**: every new control is labeled, keyboard-reachable, state changes perceivable, **never** conveyed by the copper color alone; the pilot-light ember→steady motion maps to **real** state (not a free-running animation); WCAG AA in **both** Workshop Daylight and Night Forge.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: maps the task to its user story (US1–US7). Setup / Foundational / Foundry-wiring / UI-scaffold / Polish carry no story label.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the M5 baseline before any code changes. **No package delta in M5** (Core seams are plain managed code; the route list is static data).

- [ ] T001 Confirm a green baseline: build the solution and run `dotnet test tests/FoundryStudio.Tests/FoundryStudio.Tests.csproj` from repo root; record the existing-suite pass set (incl. `NoBlockingInitGuardTests`, `ModelStateGateTests`, `RamFitHeuristicTests`, `SettingsDocumentTests`, `CatalogGroupingTests`, `DeleteConsentGateTests`, `PostV1StubTests`, …) so regressions are visible. Confirm the existing honest stub `src/FoundryStudio.Core/PostV1/StubLocalServerService.cs` (`IsSupported => false`; Start/Stop throw `"wired in M5"`) and the disabled `nav-server` `<button>` in `src/FoundryStudio.App/Components/Layout/Sidebar.razor` are present as the M5 starting point. **No** new `PackageReference` is added in M5.

**Checkpoint**: Baseline green; M5 stub + disabled nav-server confirmed as the starting point; no package changes.

---

## Phase 2: Foundational — Core lifecycle types (Blocking Prerequisites) — coordinator

**Purpose**: The pure honest-lifecycle types every Core seam, the Foundry wiring, and the UI consume. FL-free, dylib-free, in a new `src/FoundryStudio.Core/Server/` folder mirroring the M1–M4 seam precedent. These MUST land before the seam-logic phase.

**⚠️ CRITICAL**: No seam-logic or UI work begins until this phase is complete.

- [ ] T002 [P] Create `src/FoundryStudio.Core/Server/ServerState.cs` — `public enum ServerState { Stopped, Starting, Running, Stopping, Error }` (data-model.md). `Stopped` is the honest default at launch (FR-018); `Running` means `StartWebServiceAsync` succeeded **AND** `Urls` is non-empty; `Error` covers start/stop failure **or** empty `Urls` after a reported start (FR-006, R1). Derived from the real service lifecycle, never fabricated (FR-004). Pure, no FL, no I/O.
- [ ] T003 [US1] Create `src/FoundryStudio.Core/Server/ServerStatus.cs` — `public sealed record ServerStatus(ServerState State, IReadOnlyList<string> Urls, string? Message = null)` with `static ServerStatus Stopped { get; } = new(ServerState.Stopped, Array.Empty<string>())`, `bool IsRunning => State == ServerState.Running`, `bool IsBusy => State is ServerState.Starting or ServerState.Stopping`. `Urls` is **non-empty only** when `Running` (verbatim from `ILocalServerService.Urls`); an `Error`/`Stopped` status carries `Array.Empty<string>()`; `Message` names the real diagnosed cause for `Error`/busy, `null` otherwise (FR-004/006) (depends on T002).

**Checkpoint**: Core lifecycle types compile; seam-logic pairs can now proceed in parallel.

---

## Phase 3: Core pure seams + dylib-free unit tests (CI seam gate) — coordinator

**Purpose**: The FL-free, dylib-free logic seams and their xUnit coverage — the heart of M5's honesty guarantees, unit-testable in the Core-only project (FR-027, Quickstart Layer A). **TDD**: each test is written to **FAIL first**, then its seam makes it green.

**⚠️ CRITICAL**: These seams + tests are the Constitution-II evidence backbone; every UI and Foundry task downstream consumes them. Each enforces a stated honesty invariant (core-seams.md table): empty `Urls` ⇒ `null` endpoint; pilot-light lit only in `Running`; limits are data not controls; unobservable activity ⇒ omit with honest note + zero fabricated entries.

- [ ] T004 [P] [US1] Create `src/FoundryStudio.Core/Server/ServerStateMachine.cs` — `public static class ServerStateMachine` with `bool CanTransition(ServerState from, ServerState to)` and `bool PilotLightLit(ServerState state)`. Legal edges only (data-model.md): `Stopped→Starting`, `Starting→Running|Error`, `Running→Stopping`, `Stopping→Stopped|Error`, `any→Error`, `Error→Starting|Stopped`; illegal edges return `false`. `PilotLightLit(s)` is `true` **iff** `s == Running` (FR-013, SC-006). Pure, deterministic, dylib-free (depends on T002).
- [ ] T005 [P] [US1] Create dylib-free `tests/FoundryStudio.Tests/ServerStateMachineTests.cs` (SC-006/007): each legal edge `true`; a sampling of illegal edges `false`; `PilotLightLit` `true` **only** for `Running`, `false` for the other four states (the pilot-light-never-lit-outside-Running invariant). Write to FAIL first, then confirm green against T004.
- [ ] T006 [P] [US2] Create `src/FoundryStudio.Core/Server/ServerEndpoints.cs` — `public sealed record ServerRoute(string Path, string Description)` + `public static class ServerEndpoints` with `IReadOnlyList<ServerRoute> DocumentedRoutes { get; }` (the static documented OpenAI-compatible surface — `/v1/chat/completions`, `/v1/models`, `/v1/embeddings`, R3), `string? BaseUrl(IReadOnlyList<string> urls)` (primary real address, trailing slash normalized; **`null` when empty**), `IReadOnlyList<string> AllBaseUrls(IReadOnlyList<string> urls)`, `string? CopyPayload(IReadOnlyList<string> urls)` (exact base URL; `null` when empty), `string RouteUrl(string baseUrl, ServerRoute route)` (no double slash). `BaseUrl`/`CopyPayload` return the **exact** address from `urls`, **never** a hard-coded/assumed value; empty `urls` ⇒ `null` (no live/copyable endpoint while stopped — FR-008/009, US2 AC4). Routes are labeled by the UI as **documented**, not runtime-verified (FR-011). Pure, dylib-free (depends on T002).
- [ ] T007 [P] [US2] Create dylib-free `tests/FoundryStudio.Tests/ServerEndpointsTests.cs` (SC-001/003): single URL → exact base + copy payload; multiple URLs → `AllBaseUrls` presents all; **empty URLs → `BaseUrl`/`CopyPayload` `null`** (no live endpoint — the no-fabricated-endpoint invariant); `DocumentedRoutes` contains `/v1/chat/completions` + `/v1/models`; `RouteUrl` concatenates with no double slash. Write to FAIL first.
- [ ] T008 [P] [US6] Create `src/FoundryStudio.Core/Server/ServerLimitations.cs` — `public static class ServerLimitations` with `const string LocalhostOnly/NoAuth/NoLanBind/ExternalOnly` informational fact strings (data-model.md verbatim) + `static IReadOnlyList<string> All { get; }` (the four facts, ordered). Pure **data**: rendered as text; the seam exposes **no** setter and **no** control surface for capabilities FL lacks (FR-019/020/021, Constitution IV). Dylib-free (depends on T002).
- [ ] T009 [P] [US6] Create dylib-free `tests/FoundryStudio.Tests/ServerLimitationsTests.cs` (SC-002/008): `All` contains the four facts including localhost-only, no-auth, no-LAN, and "external tools only / in-app chat unaffected" — proving the limits exist as data (the DOM contract separately asserts **0** matching capability controls). Write to FAIL first.
- [ ] T010 [P] [US7] Create `src/FoundryStudio.Core/Server/RequestActivityProjection.cs` — `public sealed record RequestActivityEntry(DateTimeOffset At, string Summary)` + `public sealed record RequestLogView(bool Show, string? HonestNote, IReadOnlyList<RequestActivityEntry> Entries)` + `public static class RequestActivityProjection { RequestLogView Project(IReadOnlyList<RequestActivityEntry>? observed); }`. Contract (R2, FR-022/023): `observed == null` (FL exposes no observable activity) ⇒ `Show == false`, `HonestNote` set ("Per-request activity is not observable from Foundry Local."), `Entries` empty; `observed` empty ⇒ `Show == true`, `HonestNote == null`, zero entries (running, no traffic yet); `observed` non-empty ⇒ `Show == true`, `Entries` are **exactly** those real entries (no synthesis, no reordering, **no timer/animation source**). Pure, dylib-free (depends on T002).
- [ ] T011 [P] [US7] Create dylib-free `tests/FoundryStudio.Tests/RequestActivityProjectionTests.cs` (SC-009): `Project(null)` ⇒ not shown + honest note + **0** entries; `Project(empty)` ⇒ shown + 0 entries; `Project([e1,e2])` ⇒ shown + exactly `[e1,e2]`. Assert **no** code path fabricates an entry (the zero-fabricated-entries invariant). Write to FAIL first.
- [ ] T012 [US5] Create dylib-free `tests/FoundryStudio.Tests/ServerGateCoordinationTests.cs` (SC-007) — mirrors `LocalServerService`'s coordination logic over a **fake** `IModelStateGate` (no FL, no dylib): start/stop **serialize** through `MutateAsync(scopeKey, MutationPolicy.Drain, …)`; a conflicting in-flight mutation surfaces `ModelBusyException` → mapped to a "busy, try again" status (`ServerStatus` with `Message`); the path uses only `await` — assert **no** `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` (KI-005). This is the dylib-free proof of the gate-coordination/busy-mapping seam that `LocalServerService` (T013) implements against the real gate. Write to FAIL first (depends on T003, T004).

**Checkpoint**: All five dylib-free seam suites green (CI seam gate clean) — every honesty/concurrency invariant unit-proven before any FL or UI touches it (Quickstart Layer A; SC-001/002/003/006/007/008/009). `NoBlockingInitGuard` stays green.

---

## Phase 4: Foundry-layer wiring — `LocalServerService` (the ONLY new FL-bound piece) + DI swap — coordinator / Bishop

**Purpose**: Implement the existing seam `ILocalServerService` for real in the Foundry layer over the **single** shared `FoundryLocalManager`, coordinated by `IModelStateGate`. This is the **one** new FL-bound class; everything else is FL-free Core/UI. It follows the established `FoundryCatalogService` pattern (manager via `FoundryLifecycle.GetManagerTypedAsync`, mutations via `IModelStateGate`). **All FL types stay inside `FoundryStudio.Foundry`** (FR-024).

- [ ] T013 [US5] Create `src/FoundryStudio.Foundry/LocalServerService.cs` — `public sealed class LocalServerService : ILocalServerService` with `ctor(FoundryLifecycle lifecycle, IModelStateGate gate, ILogger<LocalServerService> logger)` (service-surface.md). `IsSupported` reflects the **real** platform/SDK capability (macOS / Apple-Silicon head where FL's web service is available, R5) — **not** hard-coded `true`. `StartAsync`: if `!IsSupported` return an honest unsupported result; `await lifecycle.ReadyAsync(ct)`; `var manager = await lifecycle.GetManagerTypedAsync(ct)` (the **one** manager — **never** `new FoundryLocalManager(...)`, FR-003, mirrors `FoundryCatalogService.cs` L160-163); `await gate.MutateAsync(ServerScopeKey, MutationPolicy.Drain, async () => await manager.StartWebServiceAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false)`; read back `Urls = manager.Urls` — if **empty**, treat as a failed/incomplete start (honest error, state `Error`, **no** fabricated endpoint, R1/FR-006); else return the real `Urls`. `StopAsync`: `await ReadyAsync` → shared manager → `MutateAsync(..., StopWebServiceAsync, ...)` → clear `Urls` (FR-005/018). `ServerScopeKey` is a stable constant so server start/stop and model load/unload contend on the **same** `IModelStateGate` singleton; `ModelBusyException` → honest "busy, try again" (FR-016). **No** `.Result`/`.Wait()` anywhere (KI-005, FR-017); **no** port argument (none exists, R1); `Urls` is always verbatim SDK value (FR-008/025). Coordination logic mirrored dylib-free by T012.
- [ ] T014 [US5] Swap the DI registration in `src/FoundryStudio.App/MauiProgram.cs` (`RegisterFoundryStudioServices`): replace `services.AddSingleton<ILocalServerService, StubLocalServerService>();` with `services.AddSingleton<ILocalServerService, LocalServerService>();` (the real exposed server over the single shared manager). `LocalServerService` resolves `FoundryLifecycle` (concrete, for `GetManagerTypedAsync`) and `IModelStateGate` — both already registered as singletons (`MauiProgram.cs` L51-55), guaranteeing the one-manager/one-gate contract. `StubLocalServerService` is **retained** in `Core/PostV1` as the honest FL-free default for non-macOS targets and the Core-only Tests project (`IsSupported == false`); the other PostV1 stubs are untouched. **No** `using Microsoft.AI.Foundry.Local`/FL types in `.App` (FR-024) (depends on T013).

**Checkpoint**: The real server starts/stops over the **single** shared `FoundryLocalManager`, coordinated via `IModelStateGate`, reading back verbatim `Urls`; FL stays behind the seam; KI-005 clean. (`LocalServerService` itself is proven on hardware in Phase 11; its coordination is unit-proven by T012.)

---

## Phase 5: UI scaffolding — nav activation, "Forge Lit" page shell, copy/pilot assets (Blocking for UI stories) — Hicks

**Purpose**: Activate the sidebar Server nav, stand up the `/server` page + view-state that consumes **only** `ILocalServerService` + the Core seams, and land the clipboard JS + pilot-light ember styles. These block the per-story UI phases.

- [ ] T015 [US1] In `src/FoundryStudio.App/Components/Layout/Sidebar.razor`, activate the Server nav (mirroring the active `nav-chat` NavLink at L26-29): replace the disabled placeholder `<button id="nav-server" data-testid="nav-server" class="sidebar-nav__row is-disabled" … disabled aria-disabled="true">…Coming soon…</button>` (L30-33) with `<NavLink id="nav-server" data-testid="nav-server" class="sidebar-nav__row" href="/server" aria-label="Server">…<span>Server</span></NavLink>`. **Invariant**: `nav-server` no longer carries `is-disabled`/`disabled`/`aria-disabled` and routes to `/server` (FR-001).
- [ ] T016 [US4] Create `src/FoundryStudio.App/Components/Pages/Server.razor` (route `/server`, `data-testid="server-page"`) + its view-state that orchestrates the "Forge Lit" panel (DESIGN §10). It injects **only** `ILocalServerService` + the Core seams + the M1 lifecycle/gate (**never** FL types, FR-024); holds the current `ServerStatus`; calls `StartAsync`/`StopAsync` and re-renders via `await InvokeAsync(StateHasChanged)` (no `.Result`/`.Wait()`, KI-005). When `IsSupported == false`, render `data-testid="server-unsupported"` and an honest disabled toggle (no dead/enabled control, R5). Composes the child components added in US1/US2/US4/US6/US7 phases (depends on T003, T004, T006, T008, T010, T014).
- [ ] T017 [P] In `src/FoundryStudio.App/wwwroot/`, add the copy-to-clipboard JS helper (used by the copy-endpoint affordance to place the exact `ServerEndpoints.CopyPayload(Urls)` string on the clipboard, confirming honestly) and the pilot-light **ember→steady** styles using the **Foundry Copper** accent (DESIGN §3.1/§10), meeting WCAG AA in **both** Workshop Daylight and Night Forge. The ember motion maps to **real** `Running` state — **not** a free-running animation; state is conveyed by text/`data-state`, not copper color alone (FR-013/014).

**Checkpoint**: `nav-server` active and routes to `/server`; the `Server` page shell renders honest unsupported state and is ready to host the per-story components; copy JS + AA pilot styles in place.

---

## Phase 6: User Story 1 — Turn on the server and see the exact bound endpoint (Priority: P1) — Hicks

**Goal**: A start/stop toggle that drives the honest lifecycle (`stopped → starting → running → stopping → stopped`), shows the **actual** bound URL(s) read back from `Urls`, and never fabricates an endpoint — empty `Urls` after a reported start surfaces an honest failed-start, not a fake host:port.

**Independent Test (DevFlow DOM)**: Open `/server`; activate `server-toggle` (start); assert `server-status` transitions `starting → running`; assert `server-endpoint` shows a **real** `host:port` from `Urls` (not a placeholder/hard-coded port); on a forced empty-`Urls` start assert `server-endpoint-empty` + `server-error` and **no** `server-endpoint`.

- [ ] T018 [US1] Create `src/FoundryStudio.App/Components/Server/ServerToggle.razor` — `data-testid="server-toggle"` start/stop control **enabled only when `IsSupported`**; bound to `ILocalServerService.StartAsync`/`StopAsync` driven through the `ServerStateMachine` transitions; renders `data-testid="server-status"` as honest **text** `stopped`/`starting`/`running`/`stopping`/`error` (never color-only, FR-004). Awaits the service and re-renders via `await InvokeAsync(StateHasChanged)`; rapid toggling shows honest transitional `starting`/`stopping` and does not interleave calls (no `.Result`/`.Wait()`, KI-005). A gate-rejected toggle (`ModelBusyException`) surfaces `data-testid="server-busy"` ("busy, try again", FR-016) (depends on T016, T004).
- [ ] T019 [US1] Add the bound-endpoint + honest failed-start surface in `Server.razor`/a small `EndpointPanel.razor` region: `data-testid="server-endpoint"` present **only** when `running`, showing the **verbatim** `ServerEndpoints.BaseUrl(Urls)` (real `host:port`, never hard-coded — SC-001); `data-testid="server-endpoint-empty"` + `data-testid="server-error"` shown when a reported start yields **empty `Urls`** (failed/incomplete start, **never** a fabricated `server-endpoint`, FR-006/R1); `server-error` names the diagnosed cause. While stopped there is no copyable live URL (depends on T016, T006).

**Checkpoint**: Start drives the honest lifecycle and shows the real bound URL; empty `Urls` is an honest failed-start, never a fake endpoint (SC-001).

---

## Phase 7: User Story 2 — Copy the endpoint and see the exposed OpenAI-compatible routes (Priority: P1) — Hicks

**Goal**: A copy-endpoint affordance that copies the **exact** bound base URL, plus the documented OpenAI-compatible route list (labeled as the documented surface when not runtime-discoverable).

**Independent Test (DevFlow DOM)**: With the server `running`, activate `server-endpoint-copy` → clipboard equals `ServerEndpoints.CopyPayload(Urls)`; assert `server-routes` lists `/v1/chat/completions` + `/v1/models` with `server-routes-documented-label` present.

- [ ] T020 [US2] Create `src/FoundryStudio.App/Components/Server/EndpointPanel.razor` — `data-testid="server-endpoint-copy"` copies the **exact** `ServerEndpoints.CopyPayload(Urls)` via the T017 clipboard helper and confirms the copy honestly (no placeholder, FR-009/SC-003); renders `data-testid="server-routes"` from `ServerEndpoints.DocumentedRoutes` (`RouteUrl(baseUrl, route)` per row) with `data-testid="server-routes-documented-label"` present when routes are not runtime-verified (FR-011); present every address from `AllBaseUrls` when `Urls` has multiple (FR-008). **No** port control anywhere — the endpoint is read-only text from `Urls` (FR-007). The copy/route surface exists **only** while `running` (depends on T016, T006).

**Checkpoint**: Copy copies the real URL; routes are listed and honestly labeled as documented; zero port control (SC-003).

---

## Phase 8: User Story 4 — "Forge Lit": the copper pilot-light tied to real running state (Priority: P1) — Hicks

**Goal**: The copper pilot-light dot is lit (steady) **only** when truly `Running`, off when stopped, an honest transitional indicator while starting/stopping, and never lit otherwise — the transparent panel's signature delight, driven by real state.

**Independent Test (DevFlow DOM)**: Assert `server-pilot-light[data-state="running"]` (lit class) appears **iff** `server-status` reads `running`; never lit in stopped/starting/stopping/error.

- [ ] T021 [US4] Create `src/FoundryStudio.App/Components/Server/PilotLight.razor` — `data-testid="server-pilot-light"` with a `data-state` attribute = the real `ServerState`; the **lit** class is applied **iff** `ServerStateMachine.PilotLightLit(state)` is `true` (i.e. `state == Running`, FR-013/SC-006). Uses the Copper accent + ember→steady motion from T017 styles mapped to **real** state (not a free-running animation, DESIGN §10 motion principle 1); state is also conveyed by `data-state`/label text, never copper color alone (FR-014) (depends on T016, T004).

**Checkpoint**: The pilot-light tracks real running state and is never lit outside `Running` (SC-006).

---

## Phase 9: User Story 6 — Plain limitations + "does not affect in-app chat" (Priority: P2) — Hicks

**Goal**: Localhost-only / no-auth / no-LAN limitations stated as informational text (never dead controls), plus the explicit scope note that the server is external-tools-only and in-app chat does not route through it and is unaffected.

**Independent Test (DevFlow DOM)**: Assert `server-limitations` (the three limits as text) and `server-scope-note` (external-tools-only / chat-unaffected) are present, with **0** auth/API-key/LAN/remote-bind controls anywhere on `server-page`.

- [ ] T022 [US6] Create `src/FoundryStudio.App/Components/Server/LimitationsNote.razor` — `data-testid="server-limitations"` rendering `ServerLimitations.LocalhostOnly`/`NoAuth`/`NoLanBind` as plain **text** (FR-019), and `data-testid="server-scope-note"` rendering `ServerLimitations.ExternalOnly` ("for external tools only; in-app chat does not use this server and is unaffected", FR-021/SC-005). These are **data/text only** — the component renders **0** auth toggle, **0** API-key/token input, **0** LAN/`0.0.0.0`/remote-bind control (capabilities FL lacks are stated as limits, never faked — FR-020, SC-002/008) (depends on T016, T008).

**Checkpoint**: Limitations + scope/independence stated as text; zero fake security/LAN controls (SC-008).

---

## Phase 10: User Story 7 — [P3] Live request indicator / log only when observable — Hicks

**Goal**: A request-log region shown **only** when FL exposes observable activity (each entry traceable to a real request), otherwise **omitted with an honest note** and **zero** fabricated entries. PINNED on hardware (R2): the default is the omitted note unless a real activity hook is found.

**Independent Test (DevFlow DOM)**: Assert **exactly one** of `server-request-log` (with only real observed entries) **or** `server-request-log-omitted` (honest note, zero entries) is present, per `RequestActivityProjection`.

- [ ] T023 [P] [US7] Create `src/FoundryStudio.App/Components/Server/RequestLog.razor` — drive the region from `RequestActivityProjection.Project(observed)` where `observed` is the **real** activity source if FL exposes one, else `null`: when `Show` ⇒ `data-testid="server-request-log"` listing **only** the real `Entries` (no synthesis/reordering/timer source); when not ⇒ `data-testid="server-request-log-omitted"` showing the honest `HonestNote` with **zero** fabricated entries (FR-022/023, SC-009). Exactly one of the two is ever present. Whether FL exposes observable activity is **PINNED** for the hardware run (R2) — the omitted note is the honest default/fallback (depends on T016, T010).

**Checkpoint**: Request log honest — only real observed entries or an honest omission note, zero fabricated entries (SC-009). US7 is P3 and does not gate the milestone.

---

## Phase 11: Polish & Milestone Close (Cross-Cutting + Constitution II Verification)

**Purpose**: Prove M5 end-to-end on real Apple Silicon — the defining proof being an **external out-of-process `curl`** — record observed FL behavior (request-activity R2, route set R3), and write the `Verified:` line. DevFlow **DOM inspection** is the sanctioned autonomous evidence path (KI-001); light/dark **screenshots** are a frontmost-window supplement, never the gate.

- [ ] T024 Confirm the **dylib-free seam suite is green** (Quickstart Layer A, CI seam gate): `ServerStateMachineTests`, `ServerEndpointsTests`, `ServerLimitationsTests`, `RequestActivityProjectionTests`, `ServerGateCoordinationTests`, the `NoBlockingInitGuard` test (KI-005), and all existing suites — `dotnet test tests/FoundryStudio.Tests/FoundryStudio.Tests.csproj` (SC-001/002/003/006/007/008/009). Zero fabricated values in any seam; no dylib loaded.
- [ ] T025 **DevFlow DOM e2e — activate + start + transparent panel** (Quickstart Layer B B1, US1/US4) on real Apple Silicon with a model loaded: open Server nav → `/server`; assert `server-page` present + `nav-server` **not** disabled; assert **0** port controls, **0** auth/API-key controls, **0** LAN/remote-bind controls on `server-page` (SC-002/008); activate `server-toggle` (start) → `server-status` `starting → running`; assert `server-endpoint` shows a **real** `host:port` from `Urls` (not a placeholder/hard-coded port); `server-pilot-light[data-state=running]` lit; `server-routes` lists `/v1/chat/completions` + `/v1/models`; `server-limitations` + `server-scope-note` present; **exactly one** of `server-request-log` / `server-request-log-omitted` present (SC-001/003/005/006/008/009). Capture the bound port from `server-endpoint` for T027.
- [ ] T026 [US2] **Copy-endpoint DOM proof** (Layer B B2, SC-003): activate `server-endpoint-copy`; assert the clipboard equals `ServerEndpoints.CopyPayload(Urls)` (the exact base URL) and the UI confirms the copy honestly.
- [ ] T027 [US3] **EXTERNAL client proof — the defining check** (Layer B B3, SC-004/SC-011): from a **separate process** (genuinely out-of-process), with `<port>` from T025, run `curl -sS http://127.0.0.1:<port>/v1/chat/completions -H 'Content-Type: application/json' -d '{"model":"<loaded-model-id>","messages":[{"role":"user","content":"Say hello in five words."}]}'` and assert a **real** OpenAI-compatible completion from the loaded model (non-empty `choices`); run `curl -sS http://127.0.0.1:<port>/v1/models` and assert a **real** model listing (not a stub). This is the milestone's defining "wow" + external proof.
- [ ] T028 [US5] **Concurrency check** (Layer B B4, SC-007): with the server running, trigger an in-app model **load/unload** (M3) while toggling the server (and vice-versa). Assert operations **serialize** through `IModelStateGate` or surface an honest `server-busy` ("busy, try again") — **0** hangs, **0** native crashes, **0** corrupted state; confirm **no** `.Result`/`.Wait()` in the server path (code review + `NoBlockingInitGuard` green). Confirms the single-manager rule (no second `FoundryLocalManager`, SC-011).
- [ ] T029 [US6] **In-app chat unaffected** (Layer B B5, SC-005): exercise in-app chat (M4) with the server **stopped**, then **running** — assert chat streams identically in both (chat never depends on the server). Confirm `server-scope-note` states this independence.
- [ ] T030 [US1] [US3] **Stop really stops + connection-refused** (Layer B B6, SC-004): activate `server-toggle` (stop) → `server-status` `stopping → stopped`; `server-pilot-light` **not** lit; endpoint/routes no longer presented as live. Re-run the T027 `curl` → assert the connection is **refused/unavailable** (the server is genuinely down).
- [ ] T031 **Clean shutdown** (Layer B B7, FR-018): quit the app with the server running; relaunch; assert `/server` opens in the honest **stopped** state (no orphaned listener survives).
- [ ] T032 [P] [US7] **Request-log honesty on hardware — PINNED** (Layer B B8, SC-009, R2): determine whether FL exposes observable request activity. **If yes**: assert the external `curl` from T027 produced a **real** entry in `server-request-log` (not timer-driven). **If no**: assert `server-request-log-omitted` shows the honest note with **0** fabricated entries.
- [ ] T033 [P] **Negative / honesty-invariant DOM sweep** (SC-002/006/008/009): assert on `server-page` there is **0** port field/slider/dropdown, **0** auth/API-key input, **0** LAN/`0.0.0.0`/remote-bind control; `server-pilot-light` lit **iff** `server-status == running`; empty-`Urls` start ⇒ `server-endpoint-empty` + `server-error` and **no** `server-endpoint` (no fabricated endpoint); exactly one of `server-request-log` / `server-request-log-omitted` with zero fabricated entries.
- [ ] T034 [P] **Accessibility AA verification + light/dark screenshots** (FR-014/026, SC-010): via DevFlow computed-style/DOM in **both** Workshop Daylight and Night Forge, confirm every new control (`server-toggle`, `server-endpoint-copy`, any request-log control) is labeled, keyboard-reachable, announces state changes; server state is conveyed by **text/label**, not the copper color alone; the pilot-light ember→steady maps to real state; WCAG AA contrast holds. Capture frontmost-window light + dark screenshots as supplementary evidence.
- [ ] T035 Update `KNOWN-ISSUES.md`: record the **PINNED** hardware findings — whether FL exposes **observable request activity** (R2; the omitted honest note is the passing fallback) and the **runtime-served route set** (R3; documented-surface labeling is the passing fallback); record FL `StartWebServiceAsync`/`StopWebServiceAsync`/`Urls` behavior observed, the connection-refused-after-Stop result, the toggle-vs-load/unload concurrency outcome; note the KI-001 DOM-evidence path used and that KI-005 stayed clean in the server path.
- [ ] T036 **Independent review** (Constitution II/III, FR-028): a reviewer who is **not** the author runs `/review` over the M5 change set (Core seams + the single `LocalServerService` + DI swap + Blazor "Forge Lit" UI + `Sidebar` activation), with explicit attention to the honesty invariants (no port/auth/LAN controls; real bound `Urls` only; empty `Urls` ⇒ honest failed-start; pilot-light lit only in `Running`; request log omitted-or-real with zero fabricated entries), the **single-manager** rule (no second `FoundryLocalManager`), gate coordination, and KI-005. Author does not self-approve.
- [ ] T037 Write the M5 milestone-close note with a **`Verified:`** line naming the checks that ran (the five dylib-free seam suites + `NoBlockingInitGuard` + existing green; the DevFlow DOM e2e B1–B9: nav-server activated → start → **real** bound URL `<host:port>` shown (DOM) → copper pilot-light lit only when running → external `curl /v1/chat/completions` returned a **real** model response and `/v1/models` a real listing → Stop refused the same curl (server down) → in-app chat identical with server off and on → toggle vs load/unload serialized via `IModelStateGate` (no hang/crash, no `.Result`/`.Wait()`) → request log [omitted with honest note | shows only real observed entries] → **0** port/auth/LAN controls → WCAG AA in both themes; single shared `FoundryLocalManager`; FL request-activity=`<obs>`, route-set=`<obs>`), and record the GO/NO-GO milestone decision (depends on T024–T036).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies; confirms the stub + disabled nav-server starting point. No package delta.
- **Foundational (Phase 2)**: after Setup — **BLOCKS everything**. T002 before T003 and before every seam (T004/T006/T008/T010).
- **Core seams (Phase 3)**: after Foundational — the coordinator-authored dylib-free backbone. **BLOCKS** the Foundry wiring and the UI that consume each seam. T003+T004 before T012.
- **Foundry wiring (Phase 4)**: after the seams/types it threads (T003 status, T004 state machine; coordination mirrored by T012). T013 before T014 (DI swap).
- **UI scaffold (Phase 5)**: after Foundational + the seams/service it consumes (T003/T004/T006/T008/T010, T014). **BLOCKS** all UI stories. T016 before every UI-story component.
- **UI stories (Phases 6–10)**: after UI scaffold; each consumes specific seams (US1↔T004/T006, US2↔T006, US4↔T004, US6↔T008, US7↔T010).
- **Polish/close (Phase 11)**: after the in-scope stories complete (T025–T031 need US1/US2/US4/US6 + the Foundry wiring on hardware; US3/US5 are verified here; US7/T032 is P3).

### Within each story

- Pure-seam tests (T005/T007/T009/T011/T012) are written to **FAIL first**, then their seam makes them green.
- Core seams before the UI that consumes them; the Foundry-layer `LocalServerService` + DI swap before the hardware e2e that drives it.
- Story complete and DevFlow-verifiable before moving to the next priority.

### Shared-file coordination (NOT parallel within a story)

- `src/FoundryStudio.App/Components/Pages/Server.razor` + its view-state are edited by T016 (scaffold), T019 (US1 endpoint/empty surface) — **serialize these edits** (and any later region added inline).
- `src/FoundryStudio.App/Components/Layout/Sidebar.razor` is edited by T015 only.
- `src/FoundryStudio.App/MauiProgram.cs` is edited by T014 only.
- `src/FoundryStudio.Foundry/LocalServerService.cs` is created by T013 only.

### Parallel opportunities

- Core seams (Phase 3): the test+impl pairs are `[P]` across distinct new files — T004/T005, T006/T007, T008/T009, T010/T011, and T012 (over the fake gate) can be authored together (T012 follows T003/T004).
- UI scaffold: T017 (wwwroot assets) ∥ T015 (Sidebar) ∥ T016 (page shell) touch distinct files.
- UI stories after scaffold: US2 (T020), US4 (T021), US6 (T022), US7 (T023) are largely independent `[P]` across distinct new components; US1 (T018 + T019) shares `Server.razor` (serialize T019).
- Polish: T032 ∥ T033 ∥ T034 (different evidence sweeps).

---

## Parallel Example: Core dylib-free seam tests + impls (after Foundational)

```bash
# Write the failing dylib-free seam tests together (different files):
Task: "ServerStateMachineTests in tests/FoundryStudio.Tests/ServerStateMachineTests.cs"
Task: "ServerEndpointsTests in tests/FoundryStudio.Tests/ServerEndpointsTests.cs"
Task: "ServerLimitationsTests in tests/FoundryStudio.Tests/ServerLimitationsTests.cs"
Task: "RequestActivityProjectionTests in tests/FoundryStudio.Tests/RequestActivityProjectionTests.cs"
Task: "ServerGateCoordinationTests in tests/FoundryStudio.Tests/ServerGateCoordinationTests.cs"

# Then implement the pure seams in parallel (different new files):
Task: "ServerStateMachine.cs in src/FoundryStudio.Core/Server/"
Task: "ServerEndpoints.cs (+ ServerRoute) in src/FoundryStudio.Core/Server/"
Task: "ServerLimitations.cs in src/FoundryStudio.Core/Server/"
Task: "RequestActivityProjection.cs in src/FoundryStudio.Core/Server/"
```

---

## Implementation Strategy

### MVP first (US1 + US2 + US3 + US4 + US5 = the P1 server toggle)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 Core seams (coordinator, dylib-free, all green) → 4. Phase 4 Foundry wiring (`LocalServerService` + DI swap) → 5. Phase 5 UI scaffold (nav + page shell + assets) → 6. US1 (toggle + real bound URL + honest failed-start) → 7. US2 (copy + routes) → 8. US4 (copper pilot-light) → then **STOP & VALIDATE** on Apple Silicon: US3 external `curl` proof + US5 concurrency + chat-unaffected + Stop-refused. This is the smallest shippable, honesty-correct, KI-005-clean v1 "wow".

### Incremental delivery

Core seams → Foundry wiring → UI scaffold → US1 → US2 → US4 → US6 (limitations + independence) → US7 (request log, P3) → Polish/close (external curl, connection-refused, concurrency, chat-unaffected, a11y both themes). Each story is independently DevFlow-verifiable and adds value without breaking the prior ones.

---

## Suggested Squad Ownership (consistent with prior milestones)

| Owner | Scope in M5 | Primary tasks |
|-------|-------------|---------------|
| **Ripley** (lead / coordinator) | Core dylib-free seams + their unit tests; milestone-close arbitration; `Verified:` sign-off + GO/NO-GO | T002–T012 (Core types/seams + tests), T037 |
| **Bishop** (FL wiring) | The single `LocalServerService` over the shared manager + gate coordination; DI swap; FL behavior on hardware | T013, T014, T027, T028, T035 |
| **Hicks** (Blazor server UI) | nav activation, `/server` "Forge Lit" page + view-state, toggle, endpoint+copy+routes, pilot-light, limitations/scope, request log, AA styling | T015–T023, T034 |
| **Drake** (tests / CI) | Dylib-free seam suite green; DevFlow DOM e2e; negative-invariant + a11y sweeps; light/dark screenshots | T001, T024, T025, T026, T029, T030, T031, T032, T033 |
| **Spunkmeyer** (PR quality + honesty review) | Independent review (reviewer ≠ author); the honesty/single-manager/KI-005 review; KNOWN-ISSUES hygiene | T035, T036 |
| **(shared)** | DevFlow e2e on Apple Silicon + the genuinely out-of-process `curl` proof + frontmost-window light/dark screenshot supplement (KI-001) | T025, T027, T034 |

> Reviewer independence (FR-028): the author of a change set must not be its sole approver — Spunkmeyer/Ripley review what Hicks/Bishop author, and vice-versa. The honesty flow (no port/auth/LAN controls, real bound URL only, pilot-light lit only when running, request log omitted-or-real) and the single-manager/KI-005 rules get an explicit independent review pass.

---

## Notes

- `[P]` = different files, no dependency on an incomplete task.
- `[Story]` labels (US1–US7) trace each task to its spec user story; Setup/Foundational/Foundry-wiring/UI-scaffold/Polish carry none.
- Pure-seam tests (T005/T007/T009/T011/T012) must **FAIL before** their implementation lands.
- Every task preserves the M5 guardrails at the top: **capability honesty** (no port/auth/LAN controls, real bound `Urls` only, empty `Urls` ⇒ honest failed-start, pilot-light lit only in `Running`, request log omitted-or-real with zero fabricated entries), **layering** (App → `ILocalServerService` + Core seams + M1 lifecycle/gate only, never FL SDK), **single shared `FoundryLocalManager`** + **gate-coordinated** (`IModelStateGate`, `ReadyAsync`) start/stop, **independence** (in-app chat unaffected), **KI-005** (no `.Result`/`.Wait()`), dylib-free seams, WCAG AA in both themes.
- The defining Constitution-II proof is the **external out-of-process `curl`** (T027) + the **connection-refused-after-Stop** check (T030) — DOM alone is necessary but not sufficient for the "wow".
- Commit after each task or logical group; stop at any checkpoint to validate the story independently via DevFlow DOM (KI-001 — DOM is the autonomous evidence path; screenshots need the window frontmost).
- US7 is P3 — its absence (request log omitted with an honest note) does **not** block M5's DoD; the omitted-note path is the honest default unless a real activity hook is found on hardware (R2).
