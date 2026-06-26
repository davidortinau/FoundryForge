# Feature Specification: M5 — Local server toggle (the v1 "wow")

**Feature Branch**: `006-m5-server-toggle`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "M5 — Local server toggle (the v1 'wow'): a user-facing toggle to expose Foundry Local's OpenAI-compatible HTTP server for EXTERNAL tools (the LM Studio 'server' feature), with full transparency — the exact bound URL, the exposed routes, a copy-endpoint affordance, a live request indicator/log, and the honest server limitations. This is the v1 lighthouse 'wow' after the M0→M4 core. The local server is for EXTERNAL tools ONLY; our own chat (M4) does NOT go through it — it uses the in-process IChatClient adapter. M5 implements the existing (currently stubbed) `ILocalServerService` over the confirmed FL SDK API (`StartWebServiceAsync`/`StopWebServiceAsync`/`string[] Urls`) using the single `FoundryLocalManager` singleton held by FoundryLifecycle. FL binds its own port and reports it via `Urls` (no port parameter exists); the server is localhost-only, no auth, no LAN bind. Surface real state only; never fake a control for a capability FL lacks. Coordinate with `IModelStateGate` for concurrency; no `.Result`/`.Wait()`. DESIGN §10 'Forge Lit' signature moment: a compact server panel with a copper pilot-light dot, exact bound URL, copy button, routes, and live request log; the server surface lives behind the sidebar 'Server' nav (currently a disabled 'Coming soon' placeholder)."

## Overview

M4 is the v1 in-process lighthouse: a real, multi-turn, streaming conversation with a locally-loaded Foundry Local model, run entirely through the in-process MEAI `IChatService` adapter — **no socket**. The sidebar **"Server"** nav still renders a disabled **"Coming soon"** placeholder (`data-testid="nav-server"`, `is-disabled`). **M5 activates it** and ships the v1 "wow": a single user-facing toggle that turns on Foundry Local's **OpenAI-compatible HTTP server so EXTERNAL tools** (curl, Open WebUI, any OpenAI-SDK client) can talk to the model the user has loaded on this Mac — with the delight being **transparency**, per DESIGN §10 "Forge Lit."

**M5 is a server for *external* tools, not a new path for our own chat.** FoundryStudio's own chat (M4) does **not** and **will not** route through this HTTP server — it uses the in-process `IChatClient` adapter over the same `FoundryLocalManager`. The server existing or running has **no effect** on in-app chat: in-app chat works identically whether the server is stopped, starting, or running. M5 must state this plainly in the UI so a user never believes the toggle is required for, or alters, their own conversations.

The seam already exists and is currently an honest stub: `FoundryStudio.Core.Abstractions.ILocalServerService { bool IsSupported; IReadOnlyList<string> Urls; Task<IReadOnlyList<string>> StartAsync(ct); Task StopAsync(ct); }`. Today `StubLocalServerService.IsSupported` is `false` and `StartAsync`/`StopAsync` throw (`"wired in M5"`). **M5 implements it for real** over the confirmed Foundry Local SDK surface: `FoundryLocalManager.StartWebServiceAsync(CancellationToken?)`, `StopWebServiceAsync(CancellationToken?)`, and `string[] Urls` (the **actual** bound URLs). There is exactly **one** `FoundryLocalManager` singleton — held by `FoundryLifecycle` (awaited via `ReadyAsync()`) and backing **both** the in-app UI and the exposed server. M5 MUST use that singleton and MUST NOT construct a second manager.

The single most important constraint of M5 is **honesty (Constitution III) and capability honesty (Constitution IV)** — and here the delight is *inseparable* from the honesty:

- **No fake port control.** FL's `StartWebServiceAsync` takes **no port parameter**: FL binds its own port and reports the real address(es) via `Urls`. The UI MUST show the **actual bound URL(s)** read back from `Urls` after start, and MUST NOT ship a "configurable port" field, slider, or any control implying the user chooses the port (FL does not expose one through this API).
- **Real limitations, surfaced — never dead controls.** FL's server is **localhost-only, no auth, no LAN/`0.0.0.0` bind**. These limits MUST appear as plain informational text describing what the server is and is not — never as a disabled/fake auth toggle, API-key field, or LAN-binding switch that suggests a capability FL lacks.
- **Only real state.** The panel shows the actually-bound URLs, the real start/stop/starting status, and real request activity **only if** FL exposes observable request activity. If request activity is **not** observably available from FL, the live request log MUST be **omitted honestly** (with a brief honest note) rather than populated with fabricated entries. No invented log lines, no fake "request received" animation.

**Concurrency (Constitution V) is load-bearing.** One singleton manager backs the UI and the server. Toggling the server (start/stop) and any in-flight UI-driven load/unload mutate or read shared native state, so M5 MUST coordinate through the existing M1 `IModelStateGate` and the `FoundryLifecycle.ReadyAsync()` gate. Server start/stop MUST await readiness and MUST NOT race a load/unload such that shared state is corrupted or a native generation is torn. No `.Result` / `.Wait()` anywhere in the server path (KI-005).

Because this is a screen-bearing milestone, success is framed around **observable UI behavior verifiable via DevFlow DOM inspection** (KI-001 — the WebView pixel screenshot needs the window frontmost, so DOM is the sanctioned autonomous evidence path), **the real external-client proof** (an external `curl` to `<bound-url>/v1/...` returns a real response from the loaded model), and **the pure-logic seams** (status state machine, URL/route presentation, copy-endpoint payload, gate coordination) that are unit-testable without a native dylib.

## Clarifications

No outstanding clarifications. All open choices were resolved using reasonable defaults derived from `docs/PLAN.md` (the **M5 — Local server feature** milestone lines 122–125; the v1 scope line 20 framing M5 as "the wow"; the singleton-concurrency contract line 75 and async-ready gate line 76; the Parity Map), `docs/DESIGN.md` (§10 "Forge Lit" signature moment — copper pilot-light dot, exact bound URL, copy button, routes, scope, limitations; Foundry Copper accent; Workshop Daylight + Night Forge themes; WCAG AA), `.specify/memory/constitution.md` (III Honesty; IV Data Preservation & Capability Honesty; V Native-Load & In-Process Discipline; II Pre-Completion Verification), the confirmed FL SDK reflection finding (`StartWebServiceAsync`/`StopWebServiceAsync`/`string[] Urls`; no port parameter; localhost-only/no-auth/no-LAN), `KNOWN-ISSUES.md` (KI-001 DOM-evidence path; KI-005 no `.Result`/`.Wait()`), and the real code in `src/` (`ILocalServerService`/`StubLocalServerService`, `IFoundryLifecycle`/`FoundryLifecycle` and its single `FoundryLocalManager`, `IModelStateGate`/`ModelStateGate` with `MutationPolicy`/`ModelBusyException`, the disabled `nav-server` row in `Sidebar.razor`). The resulting defaults are recorded in the **Assumptions** section.

> **Reconciliation note (honesty over the plan text):** `docs/PLAN.md` line 123 reads "bind `127.0.0.1:<port>` (configurable port)." The confirmed FL SDK reflection shows `StartWebServiceAsync` accepts **no port argument** and reports the real address via `Urls`. Per Constitution III/IV, this spec **supersedes the "configurable port" wording**: M5 shows the actual bound URL and ships **no** port control. This is a deliberate, recorded honesty decision, not an omission.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Turn on the local server and see the exact bound endpoint (Priority: P1)

As a developer who wants to point an external OpenAI-compatible tool at my locally-loaded model, I want to flip a single "Server" toggle and immediately see the **exact URL Foundry Local actually bound to**, so that I can copy it into curl, Open WebUI, or any OpenAI client and start making requests — with the panel telling me the real address rather than a value I had to guess or configure.

**Why this priority**: This is the headline "wow" of M5 and the smallest slice that delivers the milestone's standalone value — "I turned it on and I can see the real endpoint to point my tools at." Every other story refines it. The seam (`ILocalServerService.StartAsync` → real `Urls`) already exists as a stub; M5 implements it and builds the panel that consumes it.

**Independent Test**: On an Apple-Silicon Mac with a model loaded (via M3), open the **Server** surface (now active), activate the start toggle, and confirm via DevFlow DOM that the panel transitions stopped → starting → running, ignites the copper pilot-light (US4), and displays the **actual bound URL(s)** read back from `ILocalServerService.Urls` — not a placeholder, not a hard-coded port. The status state machine and URL presentation are independently unit-testable as pure seams over a fake `ILocalServerService` reporting synthetic `Urls`.

**Acceptance Scenarios**:

1. **Given** the loaded Foundry Local manager is ready and the Server surface is open in the stopped state, **When** the user activates the start toggle, **Then** the service calls `StartWebServiceAsync` on the single shared `FoundryLocalManager` via `ILocalServerService.StartAsync`, the panel reflects starting → running, and the **actual** bound URL(s) from `Urls` are displayed verbatim.
2. **Given** the server is running, **When** the panel renders the endpoint, **Then** it shows the real address Foundry Local bound (read from `Urls`) and presents **no** field, slider, or input implying the user chose or can change the port (FR — no fake port control).
3. **Given** the server is running, **When** the user activates the stop toggle, **Then** the service calls `StopWebServiceAsync` via `ILocalServerService.StopAsync`, the panel returns to stopped, the pilot-light extinguishes, and the endpoint/routes are no longer presented as live.
4. **Given** the server fails to start (e.g., native fault, manager not ready), **When** the failure occurs, **Then** the panel surfaces an honest error naming the diagnosed cause and remains/returns to stopped — it does **not** show a fabricated "running" state or a fake URL.
5. **Given** the Server surface, **When** reached via assistive technology in both Workshop Daylight and Night Forge themes, **Then** the toggle, status, and endpoint are labeled, keyboard-reachable, and meet WCAG AA (server state not conveyed by color alone).

---

### User Story 2 - Copy the endpoint and see the exposed OpenAI-compatible routes (Priority: P1)

As a developer wiring up an external tool, I want a one-click "copy endpoint" affordance and a clear list of the **OpenAI-compatible routes** the server exposes (e.g. `/v1/chat/completions`, `/v1/models`), so that I can paste an exact, correct base URL into my tool and know which endpoints are available without guessing.

**Why this priority**: A bound URL the user has to hand-transcribe, and routes they have to guess, undercut the "wow." Copy-to-clipboard plus an honest route list is what turns "the server is on" into "I actually connected my tool." It is P1 because the endpoint is only useful if it can be reliably copied and its surface understood.

**Independent Test**: With the server running, confirm via DevFlow DOM that a copy affordance is present and copies the exact bound base URL (and/or a route URL) to the clipboard, and that the exposed OpenAI-compatible routes are listed. The copy payload derivation (base URL from `Urls`, optional route concatenation) and the route-list presentation are independently unit-testable as pure seams. If the exact served route set is not programmatically discoverable, the panel labels the list as the **documented** OpenAI-compatible surface (honest framing), which is also unit-testable.

**Acceptance Scenarios**:

1. **Given** the server is running, **When** the user activates the copy-endpoint affordance, **Then** the exact bound base URL (from `Urls`) is copied to the clipboard and the UI confirms the copy honestly (no copy of a placeholder).
2. **Given** the server is running, **When** the panel renders, **Then** it lists the OpenAI-compatible routes the server exposes (e.g. `/v1/chat/completions`, `/v1/models`, and `/v1/embeddings` where applicable).
3. **Given** the exact route set is not programmatically known from FL, **When** the routes are listed, **Then** they are labeled as the **documented** OpenAI-compatible surface (an honest description), not falsely presented as runtime-verified per-route status.
4. **Given** the server is stopped, **When** the panel renders, **Then** the copy affordance and route list are not presented as live/active endpoints (no copyable "live" URL while stopped).
5. **Given** the copy affordance and route list, **When** reached via assistive technology, **Then** they are labeled, keyboard-reachable, and WCAG AA in both themes.

---

### User Story 3 - Prove an external tool hits the endpoint (Priority: P1)

As a developer, I want to confirm that an **external** process (e.g. `curl`) pointed at the bound URL actually reaches my loaded model and gets a real OpenAI-compatible response, so that I trust the toggle did something real and the "wow" is genuine rather than a decorative panel.

**Why this priority**: This is the proof that M5 is real and not theater. The entire value proposition is "external tools can now talk to my local model"; if an external client cannot get a real response, the feature is fake. It is P1 and is the milestone's defining end-to-end verification.

**Independent Test**: With a model loaded and the server running, issue an **external** `curl` request to `<bound-url>/v1/chat/completions` (or `/v1/models`) from a separate process and confirm a real OpenAI-compatible response from the loaded model is returned. This is an out-of-process integration check on Apple Silicon (it exercises the real native server, so it is not a dylib-free unit test); it is the SC-level external proof for M5.

**Acceptance Scenarios**:

1. **Given** a model is loaded and the server is running at a URL from `Urls`, **When** an external client issues a request to `<bound-url>/v1/chat/completions`, **Then** it receives a real, OpenAI-compatible response generated by the loaded local model.
2. **Given** the server is running, **When** an external client requests `<bound-url>/v1/models`, **Then** it receives a real model listing from Foundry Local (not a stubbed/fabricated payload).
3. **Given** the server is stopped, **When** an external client attempts the same request, **Then** the connection is refused/unavailable (the server is genuinely down — stopping really stops it), confirming the toggle reflects real server lifecycle.
4. **Given** the external request runs **while** in-app chat is also in use, **Then** both function (subject to the shared-manager concurrency rules of US5) and the in-app chat path does not depend on the server being up.

---

### User Story 4 - "Forge Lit": the transparent server panel (Priority: P1)

As a FoundryStudio user, when I turn the server on I want the designed "Forge Lit" moment — a compact panel where a copper pilot-light dot ignites (soft ember pulse settling to steady) alongside the exact bound URL, the routes, the scope, and the limitations — so that the delight and the transparency arrive together and I instantly understand exactly what I just exposed.

**Why this priority**: M5's job in v1 is to be the lighthouse "wow," and DESIGN §10 defines that wow as transparency-as-delight. The pilot-light maps to a *real* state (server running) and the panel's whole point is to show real facts. It is P1 because the milestone's identity is this signature moment; a plain switch with no transparent panel would miss the milestone's intent.

**Independent Test**: Activate the server and confirm via DevFlow DOM that a compact server panel opens containing the copper pilot-light indicator tied to real running state, the exact bound URL, the route list, an explicit scope statement, and the limitations text — and that the pilot-light is **on only when the server is actually running** (it tracks `IsRunning`, never a free-running animation). The mapping of pilot-light/panel visibility to real server state is a unit-testable pure seam; the copper styling and ember motion are DOM/visual.

**Acceptance Scenarios**:

1. **Given** the user activates the server, **When** it reaches running, **Then** a compact panel presents the copper pilot-light dot (ember pulse → steady, per DESIGN §10 motion principles), the exact bound URL, the routes, the scope statement, and the limitations — together.
2. **Given** the server is stopped or starting, **When** the panel renders, **Then** the pilot-light is not shown as steady-running (it reflects the real state — off when stopped, an honest starting state while starting), never a fabricated "lit" state.
3. **Given** the Foundry Copper accent and both themes, **When** the panel renders in Workshop Daylight and Night Forge, **Then** styling meets WCAG AA and server state is perceivable without relying on the copper color alone (text/label conveys running vs stopped).
4. **Given** the panel, **When** reached via assistive technology, **Then** the pilot-light state, URL, routes, scope, and limitations are all labeled and perceivable.

---

### User Story 5 - Server toggle is concurrency-safe with in-app load/unload (Priority: P1)

As a FoundryStudio user who may load/unload models and toggle the server around the same time, I want the server toggle to coordinate safely with the in-app model state so that toggling the server or a load/unload in flight never corrupts the single shared Foundry Local manager or tears a native generation — and I get an honest "busy, try again" rather than a hang or crash.

**Why this priority**: One `FoundryLocalManager` singleton backs both the UI and the server (PLAN line 75). Start/stop and load/unload touch shared native state; an uncoordinated race can crash natively. Constitution V makes this non-negotiable. It is P1 because a "wow" that can corrupt shared state or deadlock the UI is not shippable.

**Independent Test**: Drive concurrent operations against a fake gate/service — a server start/stop while a load/unload mutation is in flight (and vice-versa) — and confirm the operations are serialized through the existing `IModelStateGate` coordination and the `FoundryLifecycle.ReadyAsync()` gate, that a rejected operation surfaces an honest "busy, try again" (mapped from the gate's `ModelBusyException` where applicable), and that no `.Result`/`.Wait()` is used. The gate-coordination and busy-mapping logic are independently unit-testable as pure seams.

**Acceptance Scenarios**:

1. **Given** the shared manager and gate, **When** the user toggles the server, **Then** the operation first awaits `FoundryLifecycle.ReadyAsync()` and coordinates through the existing `IModelStateGate` so it does not race an in-flight load/unload on shared state.
2. **Given** an in-flight model load/unload, **When** the user attempts to toggle the server such that it would conflict with shared-state mutation, **Then** the conflict is resolved by serialization (drain) or an honest rejection ("server is busy with a model operation, try again") — never a corrupted state, hang, or native crash.
3. **Given** a server start/stop in flight, **When** a load/unload is attempted concurrently, **Then** the same coordination applies symmetrically and the UI reflects honest busy/queued state rather than silently interleaving mutations.
4. **Given** any server-path async operation, **When** it runs, **Then** it is fully async with **no** `.Result`/`.Wait()` (KI-005) and awaits readiness rather than blocking the BlazorWebView dispatcher thread.
5. **Given** the single-manager contract, **When** the server is implemented, **Then** it uses the one `FoundryLocalManager` held by `FoundryLifecycle` and never constructs a second manager.

---

### User Story 6 - Plainly-stated limitations and "does not affect in-app chat" (Priority: P2)

As a user evaluating whether to expose the server, I want the panel to plainly tell me the server's real limits — localhost-only, no authentication, no LAN/remote binding — and to clearly say the server is for **external tools** and does **not** change or route my in-app chat, so that I make an informed decision and never assume protections (or behaviors) that don't exist.

**Why this priority**: Exposing an unauthenticated local HTTP server is a security-relevant decision; misrepresenting its reach or protections would violate honesty and could mislead a user into a false sense of security or a false belief that the toggle affects their chat. It is P2 because the toggle functions without it, but shipping without the honest limitations text would be a Constitution III/IV violation.

**Independent Test**: With the panel rendered, confirm via DevFlow DOM that the limitations are stated as informational text (localhost-only; no auth; no LAN/`0.0.0.0`) and that there is **no** auth toggle, API-key field, or LAN-binding control anywhere on the surface, and that an explicit statement says the server is for external tools and does not affect in-app chat. The presence-of-text / absence-of-dead-controls assertions are DOM-verifiable; the copy strings are unit-testable.

**Acceptance Scenarios**:

1. **Given** the server panel, **When** it renders, **Then** it states plainly that the server is **localhost-only**, has **no authentication**, and does **not** bind to the LAN / `0.0.0.0` — as informational text describing real limits.
2. **Given** the server panel, **When** it renders, **Then** there is **no** authentication toggle, API-key input, token field, or LAN/remote-binding control anywhere (no dead/fake control for a capability FL lacks).
3. **Given** the server panel, **When** it renders, **Then** it explicitly states the server exists to expose the model to **external tools** and that **in-app chat does not use it** and is unaffected by whether the server is on or off.
4. **Given** the limitations and scope text, **When** reached via assistive technology, **Then** it is readable, labeled, and WCAG AA in both themes (not conveyed by color/iconography alone).

---

### User Story 7 - Live request indicator / log when activity is observable (Priority: P3)

As a developer testing my external integration, I want to see live evidence that requests are hitting the server — a request indicator or short log — **if** Foundry Local actually exposes observable request activity, so that I can confirm my tool is connecting; and if such activity is **not** observable, I want the panel to honestly omit the log rather than show me fabricated entries.

**Why this priority**: A live request log is a delightful confirmation of real traffic, but it is strictly contingent on FL exposing observable request activity. Honesty (Constitution III) forbids fabricating log lines. It is P3 because it is conditional on a capability that may not exist; the milestone is complete and honest with the log omitted if activity is not observable.

**Independent Test**: Determine whether FL exposes observable request activity. If yes, confirm via DevFlow DOM that an indicator/log updates from real observed requests (e.g., an external `curl` produces a real entry) and not on a timer/animation. If no, confirm the panel **omits** the log with a brief honest note and contains **zero** fabricated entries. The "render only real, observed activity; otherwise omit" decision is a unit-testable pure seam over a fake activity source (empty source ⇒ no log).

**Acceptance Scenarios**:

1. **Given** FL exposes observable request activity, **When** an external request hits the running server, **Then** a live indicator/log reflects that **real** request (sourced from real observed activity), never a fabricated or timer-driven entry.
2. **Given** FL does **not** expose observable request activity, **When** the panel renders, **Then** the live log is **omitted** with a brief honest note (e.g., that per-request activity is not observable), and **no** fabricated entries appear.
3. **Given** any request indicator/log that is shown, **When** the server is stopped, **Then** it does not continue to show new activity (no traffic when the server is genuinely down).
4. **Given** a request indicator/log, **When** reached via assistive technology, **Then** it is labeled and its updates are perceivable (WCAG AA), in both themes.

---

### Edge Cases

- **No model loaded when the server starts** — the toggle still starts FL's web service (the server can be up before a model is loaded); requests for inference behave per FL's real behavior. The panel must not imply a model is loaded when it is not, and must not pretend a request will succeed without one. (Server lifecycle is independent of model-load state.)
- **Manager not ready / still initializing** — the toggle awaits `FoundryLifecycle.ReadyAsync()`; it shows an honest "initializing" / not-yet-ready state rather than blocking the UI thread or throwing an unhandled error.
- **Start fails natively** — surface an honest error naming the diagnosed cause; remain stopped; never show a fabricated running state or a fake URL.
- **`Urls` is empty after a reported start** — treat as a failed/incomplete start and surface honestly (do not present an empty or fabricated endpoint as live).
- **Multiple URLs in `Urls`** — present all real bound addresses; copy affordance copies a real one (and is unambiguous about which).
- **Stop while an external client is mid-request** — stopping really stops the server; in-flight external requests may fail (the server is down). This is FL's real behavior, surfaced honestly; the in-app UI must not deadlock.
- **Toggle spammed (rapid start/stop)** — operations serialize through the gate/coordination; the UI reflects honest transitional state (starting/stopping) and does not interleave conflicting calls on the shared manager.
- **Server on, then in-app load/unload requested** — coordinated via `IModelStateGate`; either serialized or honestly rejected ("busy, try again"); never a torn native generation or corrupted shared state (US5).
- **Server toggled while in-app chat is streaming** — in-app chat is independent of the server; the server toggle must not interrupt the in-app stream, and the shared-manager mutation rules still apply (no load/unload tearing an active generation).
- **App quit with server running** — the server is stopped/disposed cleanly on app exit via the lifecycle (no orphaned listener); on next launch the panel starts in the honest stopped state.
- **External client expects a route FL does not serve** — the route list reflects only what FL exposes (or the documented OpenAI-compatible surface, labeled honestly); FoundryStudio does not claim to serve routes FL does not.

## Requirements *(mandatory)*

### Functional Requirements

**Activate the Server surface + start/stop toggle (US1)**

- **FR-001**: The app MUST activate the sidebar **"Server"** nav (replacing the disabled `nav-server` "Coming soon" placeholder) and present the server surface/panel.
- **FR-002**: M5 MUST implement `ILocalServerService` for real (replacing the throwing `StubLocalServerService` for the macOS app): `IsSupported` MUST reflect the real platform/SDK capability, `StartAsync` MUST call Foundry Local's `StartWebServiceAsync`, `StopAsync` MUST call `StopWebServiceAsync`, and `Urls` MUST return the **actual** bound URL(s) reported by Foundry Local.
- **FR-003**: The implementation MUST use the single `FoundryLocalManager` held by `FoundryLifecycle` (obtained after `ReadyAsync()`), backing both the UI and the exposed server; it MUST NOT construct a second `FoundryLocalManager`.
- **FR-004**: The surface MUST present a start/stop toggle and reflect honest server status — **stopped / starting / running** (and an honest stopping/error state) — mapped from the real service lifecycle, never a fabricated status.
- **FR-005**: Activating start MUST start FL's web service and, on success, the panel MUST display the **actual** bound URL(s) read back from `ILocalServerService.Urls`; activating stop MUST stop the service and the panel MUST cease presenting the endpoint/routes as live.
- **FR-006**: A start failure MUST surface an honest error naming the diagnosed cause and leave/return the surface to the stopped state; the UI MUST NOT show a fabricated "running" state or a fabricated URL.

**No fake port control; show the real bound URL (US1 — honesty)**

- **FR-007**: The UI MUST NOT present any port field, slider, dropdown, or other control implying the user selects or changes the server port — Foundry Local's `StartWebServiceAsync` accepts no port parameter; the bound address is determined by FL and reported via `Urls`.
- **FR-008**: The displayed endpoint MUST be the verbatim address(es) from `Urls` (the real bound URL), not a hard-coded, assumed, or user-entered value; when `Urls` reports multiple addresses, all real addresses MUST be presented.

**Copy endpoint + exposed routes (US2)**

- **FR-009**: The panel MUST provide a copy-endpoint affordance that copies the **exact** bound base URL (from `Urls`) to the clipboard and confirms the copy honestly; it MUST NOT copy a placeholder or a value not derived from `Urls`.
- **FR-010**: The panel MUST list the OpenAI-compatible routes the server exposes (e.g. `/v1/chat/completions`, `/v1/models`, and `/v1/embeddings` where applicable).
- **FR-011**: If the exact served route set is not programmatically discoverable from Foundry Local, the route list MUST be labeled as the **documented** OpenAI-compatible surface (an honest description) rather than presented as per-route runtime-verified status.

**Forge Lit transparent panel (US4)**

- **FR-012**: Turning the server on MUST open the compact "Forge Lit" panel (DESIGN §10) presenting, together: the copper pilot-light dot, the exact bound URL, the routes, the scope statement, and the limitations.
- **FR-013**: The pilot-light indicator MUST track the **real** running state (steady only when the server is actually running; off when stopped; an honest transitional state while starting) — it MUST NOT be a free-running/fabricated animation disconnected from server state.
- **FR-014**: The panel MUST use the Foundry Copper accent and meet WCAG AA in both **Workshop Daylight** and **Night Forge** themes, conveying running-vs-stopped state by text/label (not by the copper color alone).

**Concurrency safety with the shared manager (US5)**

- **FR-015**: Server start/stop MUST await `FoundryLifecycle.ReadyAsync()` before touching the manager and MUST coordinate with the existing M1 `IModelStateGate` so a server toggle and an in-flight load/unload do not race on the single shared manager's state.
- **FR-016**: When a server toggle and a model load/unload conflict on shared state, the conflict MUST be resolved by serialization (drain) or an honest rejection surfaced to the user as a "busy, try again" state (mapped from the gate's `ModelBusyException` where applicable) — never a corrupted state, hang, or native crash.
- **FR-017**: No `.Result` / `.Wait()` (or other sync-over-async blocking) may be used anywhere in the server path (KI-005); all server operations MUST be fully async and MUST NOT block the BlazorWebView dispatcher thread.
- **FR-018**: The server MUST be stopped/disposed cleanly on app exit through the lifecycle so no orphaned listener survives; the surface MUST start in the honest stopped state on next launch.

**Plain limitations + independence from in-app chat (US6)**

- **FR-019**: The panel MUST plainly state the server's real limitations as informational text: **localhost-only**, **no authentication**, and **no LAN / `0.0.0.0` binding**.
- **FR-020**: The surface MUST NOT present any authentication toggle, API-key/token input, or LAN/remote-binding control — no dead or fake control for a capability Foundry Local lacks (Constitution IV).
- **FR-021**: The panel MUST explicitly state that the server is for **external tools** and that **in-app chat does not route through it** and is unaffected by whether the server is running; in-app chat behavior MUST be identical whether the server is stopped, starting, or running.

**Live request indicator / log — only if observable (US7)**

- **FR-022**: A live request indicator/log MUST be shown **only if** Foundry Local exposes observable request activity, and when shown it MUST reflect only **real** observed requests (never timer-driven, animated, or fabricated entries).
- **FR-023**: If Foundry Local does **not** expose observable request activity, the live log MUST be **omitted** with a brief honest note, and the panel MUST contain **zero** fabricated request entries.

**Cross-cutting (honesty, layering, accessibility, verification)**

- **FR-024**: The server UI MUST consume only `ILocalServerService` (and existing M1 lifecycle/gate seams); it MUST NOT call the Foundry Local SDK directly from the UI layer (Constitution V / layering).
- **FR-025**: M5 MUST NOT show any fabricated status, URL, route status, request entry, or capability control; every value shown MUST come from the real service/SDK, and genuinely-absent capabilities (port choice, auth, LAN, unobservable request activity) MUST be omitted or stated as limits — never faked (Constitution III/IV).
- **FR-026**: All new interactive controls (the start/stop toggle, copy-endpoint affordance, any request-log control) MUST meet WCAG AA — labeled, keyboard-reachable, state changes perceivable, no information by color alone — in both Workshop Daylight and Night Forge themes.
- **FR-027**: The pure-logic seams introduced by M5 (the status state machine; URL/route presentation; copy-endpoint payload derivation from `Urls`; gate-coordination and busy-mapping; the "render only observed activity, else omit" decision) MUST be unit-testable without a native dylib.
- **FR-028**: M5 MUST close with a real Apple-Silicon DevFlow end-to-end check — model loaded → server started → exact bound URL/routes shown (observed via DOM per KI-001) → **an external `curl` to `<bound-url>/v1/...` returns a real response from the loaded model** → stop verified (connection refused after stop) — and a `Verified:` line; the reviewer MUST be independent of the author (Constitution II/III).

### Key Entities *(include if feature involves data)*

- **ILocalServerService** (existing seam, M5 implements): `IsSupported`, `Urls` (the **real** bound addresses), `StartAsync`/`StopAsync`. The UI's only gateway to the exposed server. Backed by the single `FoundryLocalManager`.
- **FoundryLocalManager** (FL SDK singleton, held by `FoundryLifecycle`): the one manager backing both the UI and the server. Exposes `StartWebServiceAsync(CancellationToken?)`, `StopWebServiceAsync(CancellationToken?)`, and `string[] Urls`. Never duplicated.
- **ServerStatus**: the honest lifecycle state — stopped / starting / running / stopping / error — derived from the real service, display-only; never fabricated.
- **BoundEndpoint**: the verbatim URL(s) from `Urls` plus the copyable base URL payload. The real address FL chose; the user never picks the port.
- **ExposedRoutes**: the OpenAI-compatible route list (e.g. `/v1/chat/completions`, `/v1/models`, `/v1/embeddings` where applicable), labeled as runtime-served or as the documented surface (honest framing).
- **ServerLimitations**: informational facts — localhost-only, no auth, no LAN bind — presented as text, never as controls.
- **RequestActivity** (conditional): real observed request entries/indicator, present only if FL exposes observable activity; otherwise absent (no fabricated entries).
- **IModelStateGate** (existing, M1): coordinates server start/stop with model load/unload over the shared manager; `MutationPolicy` (Drain/Reject) and `ModelBusyException` map to the honest "busy, try again" state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with a loaded model can turn the server on from the activated **Server** nav, and the panel displays the **actual** bound URL(s) read from `Urls` (verified via DOM on Apple Silicon) in 100% of successful starts — never a placeholder and never a hard-coded port.
- **SC-002**: The surface ships **0** port-selection controls, **0** auth/API-key controls, and **0** LAN/remote-binding controls (no fake control for a capability FL lacks), verified via DOM — capabilities FL lacks are omitted or stated as limits, not faked.
- **SC-003**: The copy-endpoint affordance copies the exact bound base URL (from `Urls`) to the clipboard in 100% of running-state copies, and the OpenAI-compatible routes (`/v1/chat/completions`, `/v1/models`, …) are listed (and, when not runtime-discoverable, labeled as the documented surface).
- **SC-004**: **External-client proof** — with the server running, an external `curl` to `<bound-url>/v1/chat/completions` returns a real OpenAI-compatible response from the loaded model, and `<bound-url>/v1/models` returns a real model listing; after Stop, the same external request is refused/unavailable — all verified on Apple Silicon.
- **SC-005**: In-app chat (M4) behaves identically whether the server is stopped, starting, or running, and the panel explicitly states the server is for external tools and does not route in-app chat — verified by exercising in-app chat with the server both off and on (in-app chat never depends on the server being up).
- **SC-006**: The "Forge Lit" panel shows the copper pilot-light tied to real running state (steady only when running; off when stopped) with the URL, routes, scope, and limitations together; the pilot-light is **never** lit while the server is not running, verified via DOM in both themes.
- **SC-007**: Server start/stop and in-app load/unload are concurrency-safe over the single shared manager: a conflicting operation is serialized or honestly rejected ("busy, try again") with **0** corrupted-state/hang/native-crash occurrences and **0** uses of `.Result`/`.Wait()` in the server path — proven by a dylib-free unit test of the gate-coordination/status seam plus the Apple-Silicon run.
- **SC-008**: The limitations (localhost-only, no auth, no LAN bind) are stated as informational text in 100% of running-panel renders, with **0** dead/fake controls implying those capabilities, verified via DOM.
- **SC-009**: The live request log is shown **only** when FL exposes observable activity (each entry traceable to a real request) or is **omitted with an honest note**, with **0** fabricated entries in either case.
- **SC-010**: All new server controls meet WCAG AA (labeled, keyboard-reachable, state perceivable, not color-only) in both Workshop Daylight and Night Forge themes.
- **SC-011**: M5 closes with a real Apple-Silicon DevFlow end-to-end run (load → start → real URL/routes shown → external `curl` returns a real model response → stop refuses the request, observed via DOM/out-of-process curl) and a `Verified:` line, reviewed by an independent reviewer, using the single shared `FoundryLocalManager` (no second manager constructed).

## Assumptions

- **External tools only**: the server exposes the loaded model to external OpenAI-compatible clients; FoundryStudio's own chat (M4) uses the in-process `IChatClient` adapter and never routes through this server. The server's existence/running state has no effect on in-app chat. (PLAN line 113/122; constraint confirmed.)
- **Confirmed FL SDK surface**: `FoundryLocalManager.StartWebServiceAsync(CancellationToken?)`, `StopWebServiceAsync(CancellationToken?)`, and `string[] Urls` are the real API (confirmed by reflection). There is **no** port parameter; FL binds its own port and reports the real address via `Urls`. These are treated as confirmed facts, not re-litigated.
- **"Configurable port" wording is superseded**: `docs/PLAN.md` line 123's "configurable port" predates the reflection finding; per Constitution III/IV this spec ships the real bound URL with **no** port control (recorded in the Clarifications reconciliation note).
- **Real FL limitations are binding**: the FL server is localhost-only, has no auth, and does not bind the LAN/`0.0.0.0`. These are surfaced as plain limits; no auth/LAN/TLS/CORS/rate-limit control is shipped (none exist in FL).
- **Single shared manager**: the one `FoundryLocalManager` held by `FoundryLifecycle` backs both UI and server; M5 obtains it via `ReadyAsync()`/`GetManagerAsync()` and never constructs a second. Concurrency is coordinated through the existing M1 `IModelStateGate` (`MutationPolicy` Drain/Reject; `ModelBusyException`).
- **Request-log is conditional**: a live request indicator/log ships only if FL exposes observable request activity; if it does not, the log is omitted honestly. The in-scope path does not depend on a request log existing.
- **Server lifecycle is independent of model-load state**: the web service can be started/stopped regardless of whether a model is loaded; inference requests over the server behave per FL's real behavior. The panel does not imply a loaded model when there is none.
- **Verification path**: per KI-001, DOM inspection via DevFlow is the sanctioned autonomous evidence path for the panel; the external-client proof is an out-of-process `curl` on Apple Silicon (it exercises the real native server and is not a dylib-free unit test). Pure seams (status state machine, URL/route/copy presentation, gate coordination, observed-activity-only logic) are verified by dylib-free unit tests.
- **No sync-over-async**: per KI-005, the entire server toggle/lifecycle path is fully async with no `.Result`/`.Wait()` and does not block the BlazorWebView dispatcher thread.
- **Out of scope (post-v1 / non-existent)**: configurable-port UI, auth/API-key UI, LAN/`0.0.0.0` binding, request rate-limiting, CORS config, TLS, RAG, voice, presets, and MCP — none ship in M5; no control is added for a capability Foundry Local lacks.
