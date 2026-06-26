# Implementation Plan: M5 — Local server toggle (the v1 "wow")

**Branch**: `006-m5-server-toggle` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-m5-server-toggle/spec.md`

## Summary

M5 activates the sidebar **Server** nav (today a disabled `nav-server` "Coming soon" `<button>` in `Sidebar.razor`) and ships the v1 lighthouse **"wow"**: a single user-facing toggle that turns on Foundry Local's **OpenAI-compatible HTTP server for EXTERNAL tools** (curl, Open WebUI, any OpenAI-SDK client) — with the delight being **transparency** (DESIGN §10 "Forge Lit"): a copper pilot-light dot, the **exact bound URL(s)** read back from `Urls`, a copy affordance, the OpenAI-compatible route list, the scope statement, and the honest limitations. The server is for external tools only; **in-app chat (M4) does not and will not route through it** — it stays on the in-process `IChatService`/`FoundryChatClient` adapter and behaves identically whether the server is stopped, starting, or running.

M5 implements the existing honest stub `ILocalServerService` (`src/FoundryStudio.Core/PostV1/StubLocalServerService.cs` — today `IsSupported => false`, Start/Stop throw `"wired in M5"`) for real, in the **Foundry layer**, over the confirmed FL SDK surface: `FoundryLocalManager.StartWebServiceAsync(CancellationToken?)`, `StopWebServiceAsync(CancellationToken?)`, and `string[] Urls` (the **actual** bound URLs — there is **no port parameter**). The impl wraps the **single** `FoundryLocalManager` obtained via `FoundryLifecycle.GetManagerTypedAsync()` (the same `ReadyAsync()`-gated singleton backing the catalog and chat — `FoundryCatalogService.cs` L160-163 is the precedent) and **coordinates start/stop with the existing M1 `IModelStateGate`** so a server toggle never races an in-flight load/unload on shared native state. No `.Result`/`.Wait()` anywhere (KI-005).

**Technical approach**: M5 is a **UI + pure-logic-seam** milestone with exactly **one** new FL-bound piece. New work lands as: (a) **Core pure seams** (FL-free, dylib-free, unit-tested in `tests/FoundryStudio.Tests`) — a `ServerState` enum + `ServerStatus` record, a `ServerStateMachine` pure transition validator, a `ServerEndpoints` helper (derive the copy-friendly base URL + the documented OpenAI-compatible route list from `Urls`), a `ServerLimitations` constant data set (localhost-only / no-auth / no-LAN as **data**, never controls), and a `RequestActivityProjection` "render only observed activity, else omit" decision seam over a fake activity source; (b) **Foundry-layer wiring** — one new class `LocalServerService : ILocalServerService` that awaits `ReadyAsync()`, calls `StartWebServiceAsync`/`StopWebServiceAsync` on the shared manager, reads `Urls` back, and serializes start/stop through `IModelStateGate.MutateAsync(...)` — the **only** new FL-bound code; (c) **Blazor UI** in `.App` (consuming **only** `ILocalServerService` + the Core seams + M1 lifecycle/gate — **never** FL types) — a `Server.razor` "Forge Lit" panel behind an activated `nav-server`, with start/stop toggle, copper pilot-light tied to real running state, exact bound URL + copy, route list, scope + limitations text, and a request-log region that is honestly omitted if FL exposes no observable activity. Verification follows Constitution II: dylib-free unit tests for every seam **plus** a real Apple-Silicon DevFlow run whose defining proof is an **external** `curl http://127.0.0.1:<port>/v1/chat/completions` returning a real response from the loaded model, and a connection-refused check after Stop.

## Technical Context

**Language/Version**: C# 13 / .NET 11 (`net11.0-macos` for `.App`; `net11.0` for `.Core`/`.Foundry`/`.Tests`), pinned via `global.json` / `Directory.Packages.props`. (The net10 Sherpa baseline in `Directory.Packages.props` is `Condition=false` reference-only.)

**Primary Dependencies**: .NET MAUI Blazor Hybrid (AppKit head); Microsoft.AI.Foundry.Local `1.2.3` (behind the Foundry layer only — supplies `FoundryLocalManager.StartWebServiceAsync`/`StopWebServiceAsync`/`Urls`); Microsoft.Maui.DevFlow `0.25.0-dev` for DOM verification (KI-001). **No new package references** — M5 adds no library dependency (the Core seams are plain managed code; the route list is static data).

**Storage**: **None new.** M5 introduces no persistent store. Server lifecycle is in-memory/native only; on next launch the panel starts in the honest **stopped** state (FR-018). No change to the model cache, `settings.json`, or chat history.

**Testing**: xUnit in `tests/FoundryStudio.Tests` (Core-only, dylib-free) — unit tests for the state machine, `ServerEndpoints` (copy payload + route derivation), `ServerLimitations` data, the `RequestActivityProjection` (empty source ⇒ no log), and the gate-coordination/busy-mapping seam over a fake gate. MAUI DevFlow DOM inspection on real Apple Silicon for UI verification + the **external `curl`** out-of-process integration proof (KI-001 sanctioned evidence path; the curl is the SC-004/SC-011 external proof).

**Target Platform**: macOS / Apple Silicon only. No iOS/Android/Mac Catalyst. FL server is localhost-only.

**Project Type**: Desktop app — the existing 4-project MAUI Blazor Hybrid solution (App, Core, Foundry, Tests). **No new projects.**

**Performance Goals**: Start/stop are fully async off the BlazorWebView dispatcher; the UI shows honest transitional state (starting/stopping) and re-renders via `await InvokeAsync(StateHasChanged)`, never blocking (KI-005). The pilot-light tracks real `Running` state, never a free-running animation. The external `curl` round-trip latency is FL's real behavior, not animated.

**Constraints**: **Honesty (Constitution III/IV)** — **no** port field/slider/dropdown (FL exposes no port parameter; show verbatim `Urls`); **no** auth toggle, API-key field, or LAN/`0.0.0.0` bind control (FL lacks them — surface as plain limit text, never dead controls); the request log is shown **only if** FL exposes observable activity, otherwise **omitted with an honest note** and **zero** fabricated entries; an empty `Urls` after a reported start is treated as a failed/incomplete start, not a fake endpoint. **Layering (Constitution V)** — the server UI consumes only `ILocalServerService` + M1 seams, never the FL SDK (FR-024); all FL types stay behind the Foundry layer; exactly **one** `FoundryLocalManager` (no second manager). **Concurrency (Constitution V)** — start/stop await `ReadyAsync()` and serialize through `IModelStateGate`; a conflict drains or surfaces an honest "busy, try again" (mapped from `ModelBusyException`); **no** `.Result`/`.Wait()` (KI-005, FR-017) — the `NoBlockingInitGuard` test stays green. **Independence** — in-app chat is unaffected by server state (FR-021). **Accessibility** — WCAG AA on every new control in both Workshop Daylight and Night Forge; state never conveyed by the copper color alone.

**Scale/Scope**: 7 in-scope user stories (5×P1, 1×P2, 1×P3) + edge cases; ~5 new Core seams (state machine, status model, endpoints helper, limitations data, request-activity projection); **1** new Foundry-layer class (`LocalServerService`, the only new FL-bound piece); ~1 new Blazor page + ~4 small components + sidebar nav activation + DI swap (stub → real on the macOS head); ~5 new unit-test classes. No new projects, no new packages, no new persistent store.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Evidence / How M5 complies |
|-----------|--------|----------------------------|
| **I. Citation Before Action** | **PASS** | Every design claim cites real code or a confirmed finding: the seam `ILocalServerService.cs` and stub `StubLocalServerService.cs` (`"wired in M5"`); the singleton access pattern `FoundryLifecycle.GetManagerTypedAsync()` + `FoundryCatalogService.cs` L160-163; the gate `IModelStateGate`/`ModelStateGate.cs` (`MutateAsync`, `MutationPolicy.Drain/Reject`, `ModelBusyException`); the disabled `nav-server` `<button>` in `Sidebar.razor` L30-33 and the activated `nav-chat` NavLink L26-29 as the activation precedent; DI in `MauiProgram.cs` L51-70 (`AddSingleton<ILocalServerService, StubLocalServerService>()`); DESIGN §10 "Forge Lit" (L290-292) + §3.1 Copper; the **confirmed FL reflection finding** (`StartWebServiceAsync`/`StopWebServiceAsync`/`string[] Urls`; no port parameter; localhost-only/no-auth/no-LAN); `KNOWN-ISSUES.md` (KI-001/005/007). The two genuine runtime unknowns — (1) whether FL exposes **observable request activity**, and (2) the exact **runtime-served route set** — are flagged in research.md and **pinned for the DevFlow/hardware run**, not guessed; the spec already mandates the honest fallback (omit the log; label routes as the documented surface). |
| **II. Pre-Completion Verification (NON-NEGOTIABLE)** | **PASS (planned)** | quickstart.md defines **Layer A** dylib-free unit tests for every seam **and Layer B** a real Apple-Silicon DevFlow DOM e2e whose defining proof is an **external out-of-process `curl http://127.0.0.1:<port>/v1/chat/completions`** returning a real model response, plus `/v1/models`, plus a **connection-refused-after-Stop** check, plus a **concurrency check** (toggle vs load/unload), plus an **in-app-chat-unaffected** check (chat works with server off and on). M5 closes with a `Verified:` line; reviewer independent of author (FR-028). DOM is the sanctioned autonomous evidence path (KI-001); the curl is genuinely out-of-process. |
| **III. Surgical Changes & Reviewer Independence** | **PASS** | Scope is additive and bounded: 5 new Core seams, **1** new Foundry class (`LocalServerService`), a new Blazor page + small components, the `Sidebar.razor` `nav-server` activation mandated by FR-001, and a one-line DI swap (stub → real on the macOS head). No opportunistic refactor of M1–M4 code; the stub was explicitly left for this milestone. The **honesty reconciliation** (no "configurable port" despite `docs/PLAN.md` L123) is recorded in the spec's Clarifications and re-stated in research.md (R1). Author does not self-approve. |
| **IV. Data Preservation & Capability Honesty** | **PASS (load-bearing)** | **No user data is touched** — M5 adds no store and wipes nothing (no consent dialog needed). **Capability honesty is the milestone's core**: **0** port controls (FL has no port param → show verbatim `Urls`; empty `Urls` ⇒ honest failed-start, never a fake endpoint); **0** auth/API-key/LAN-bind controls (FL lacks them → plain limit text, never dead toggles); the request log appears **only** for real observed activity or is **omitted with an honest note** (zero fabricated entries); routes are labeled as the **documented** OpenAI-compatible surface if not runtime-discoverable; the pilot-light is lit **only** when truly running. Every value shown comes from the real service/SDK (FR-025). |
| **V. Native-Load & In-Process Discipline** | **PASS** | The server UI consumes only `ILocalServerService` + M1 lifecycle/gate seams; **all** FL types stay behind the Foundry layer (`LocalServerService` is the only new FL-bound code) (FR-024). It uses the **one** `FoundryLocalManager` via `FoundryLifecycle.GetManagerTypedAsync()` and never constructs a second (FR-003). Start/stop await `ReadyAsync()` and serialize through the single `IModelStateGate` so a concurrent load/unload drains or is honestly rejected (`ModelBusyException` → "busy, try again") rather than tearing native state (FR-015/016). In-app chat stays in-process — the server is **external-tools only** and has no effect on chat (FR-002/021). Fully async, no `.Result`/`.Wait()` (KI-005, FR-017). |

**Initial gate**: ✅ PASS (no violations). **Post-Design re-check (Phase 1)**: ✅ PASS — M5 adds **no** new abstraction beyond the already-existing `ILocalServerService` seam, **no** new project, **no** new package, **no** persistent store; the single new FL-bound class mirrors the established `FoundryCatalogService` pattern (manager via `GetManagerTypedAsync`, mutations via `IModelStateGate`). No Complexity Tracking entry required.

## Project Structure

### Documentation (this feature)

```text
specs/006-m5-server-toggle/
├── plan.md              # This file
├── research.md          # Phase 0 — StartAsync→StartWebServiceAsync+Urls mapping; observable-request-activity decision (PINNED for hardware); route-list source; IModelStateGate coordination; IsSupported determination
├── data-model.md        # Phase 1 — ServerState/ServerStatus, ServerEndpoints, ServerRoute list, ServerLimitations, RequestActivity (conditional)
├── quickstart.md        # Phase 1 — Layer A dylib-free unit tests + Layer B Apple-Silicon e2e incl. EXTERNAL curl proof + concurrency + chat-unaffected checks
├── contracts/           # Phase 1
│   ├── core-seams.md             # Exact signatures: ServerState/ServerStatus, ServerStateMachine, ServerEndpoints, ServerLimitations, RequestActivityProjection
│   ├── service-surface.md        # ILocalServerService impl contract (LocalServerService) + manager access + gate coordination + DI swap
│   └── server-ui.dom.md          # DOM id/data-testid hooks + honesty/concurrency/accessibility invariants for DevFlow
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root) — extend the existing 4 projects, no new projects

```text
src/FoundryStudio.Core/                       # FL-free, dylib-free (no new package references)
├── Abstractions/
│   └── ILocalServerService.cs     # (existing; unchanged — IsSupported / Urls / StartAsync / StopAsync seam)
├── Server/                         # NEW folder — pure server-presentation seams
│   ├── ServerState.cs             # NEW — enum { Stopped, Starting, Running, Stopping, Error }
│   ├── ServerStatus.cs            # NEW — record { ServerState State, IReadOnlyList<string> Urls, string? Message }
│   ├── ServerStateMachine.cs      # NEW — pure transition validator (Stopped→Starting→Running→Stopping→Stopped; any→Error)
│   ├── ServerEndpoints.cs         # NEW — base URL(s) from Urls + copy payload + documented OpenAI-compatible route list
│   ├── ServerLimitations.cs       # NEW — static data: localhost-only / no-auth / no-LAN (informational facts, never controls)
│   └── RequestActivityProjection.cs # NEW — "render only observed activity, else omit" decision over a fake activity source
├── PostV1/
│   └── StubLocalServerService.cs  # (existing; retained as the non-macOS/default honest stub — IsSupported false)
└── Concurrency/ModelStateGate.cs  # (existing; reused — server start/stop coordinate via IModelStateGate)

src/FoundryStudio.Foundry/                    # FL behind the seam — the ONLY new FL-bound piece
└── LocalServerService.cs          # NEW — ILocalServerService over the shared FoundryLocalManager
                                   #   • ctor(FoundryLifecycle, IModelStateGate, ILogger)
                                   #   • IsSupported (real platform/SDK capability)
                                   #   • StartAsync: await ReadyAsync → gate-coordinate → StartWebServiceAsync → read Urls back
                                   #   • StopAsync:  await ReadyAsync → gate-coordinate → StopWebServiceAsync
                                   #   • no second manager; no .Result/.Wait()

src/FoundryStudio.App/
├── Components/
│   ├── Layout/Sidebar.razor       # CHANGED — activate nav-server (disabled <button> → NavLink href="/server"); remove "Coming soon"
│   ├── Pages/Server.razor         # NEW — route /server; orchestrates the "Forge Lit" panel; toggle + honest status
│   └── Server/
│       ├── ServerToggle.razor     # NEW — start/stop toggle; honest stopped/starting/running/stopping/error + "busy, try again"
│       ├── EndpointPanel.razor    # NEW — exact bound URL(s) from Urls + copy-endpoint affordance + route list (no port control)
│       ├── PilotLight.razor       # NEW — copper pilot-light dot tied to real Running state (ember→steady); off when stopped
│       ├── LimitationsNote.razor  # NEW — localhost-only / no-auth / no-LAN + "external tools only; in-app chat unaffected"
│       └── RequestLog.razor       # NEW — real observed activity OR honest "not observable" note (zero fabricated entries)
├── MauiProgram.cs                 # CHANGED — swap AddSingleton<ILocalServerService, StubLocalServerService>() → real LocalServerService
└── wwwroot/                       # CHANGED — copy-to-clipboard JS + pilot-light ember styles (Copper accent, AA, both themes)

tests/FoundryStudio.Tests/         # Core-only, dylib-free
├── ServerStateMachineTests.cs         # NEW — valid/invalid transitions; pilot-light-lit ⇔ Running only (SC-006/SC-007)
├── ServerEndpointsTests.cs            # NEW — copy payload from Urls; multi-URL; documented route list; empty Urls ⇒ no live endpoint (SC-001/003)
├── ServerLimitationsTests.cs          # NEW — limitations present as data; zero capability controls implied (SC-002/008)
├── RequestActivityProjectionTests.cs  # NEW — empty/unobservable source ⇒ no log; observed entries ⇒ only-real (SC-009)
└── ServerGateCoordinationTests.cs     # NEW — start/stop serialize via fake IModelStateGate; ModelBusyException → busy-state; no blocking (SC-007)
```

**Structure Decision**: Single MAUI Blazor Hybrid desktop solution; extend the four existing projects (`FoundryStudio.App`, `.Core`, `.Foundry`, `.Tests`). All pure server-presentation logic lives in `.Core` (FL-free, dylib-free, unit-tested) in a new `Server/` folder, mirroring the M1–M4 seam precedent. The **single** FL-bound class `LocalServerService` lives in `.Foundry` and follows the established `FoundryCatalogService` pattern (manager via `FoundryLifecycle.GetManagerTypedAsync`, mutation coordination via `IModelStateGate`). The server UI in `.App` consumes only `ILocalServerService` + the M1 lifecycle/gate seams + the new Core seams — never the FL SDK (Constitution V / DEC-004 layering). The existing `StubLocalServerService` is **retained** as the honest non-macOS/default registration; the macOS head swaps to the real impl in `MauiProgram.cs`. No new projects, no new packages, no persistent store (Complexity Tracking not triggered).

## Complexity Tracking

> No Constitution Check violations. No entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | — | — |
