# Contract: Server UI DOM hooks + honesty/concurrency/accessibility invariants

The server surface in `FoundryForge.App` consumes **only** `ILocalServerService` + the Core seams (`ServerState`/`ServerStatus`, `ServerStateMachine`, `ServerEndpoints`, `ServerLimitations`, `RequestActivityProjection`) + the M1 lifecycle/gate — **never** FL types (FR-024). Stable `id` / `data-testid` hooks make every assertion DevFlow-DOM-verifiable (KI-001 is the sanctioned autonomous evidence path; the WebView pixel screenshot needs the window frontmost, so DOM is the evidence channel).

---

## Sidebar activation (FR-001)

`src/FoundryForge.App/Components/Layout/Sidebar.razor`

Replace the disabled placeholder (today L30-33):
```razor
<button id="nav-server" data-testid="nav-server" class="sidebar-nav__row is-disabled" type="button" disabled aria-disabled="true">
  <span>Server</span><span class="sidebar-nav__soon">Coming soon</span>
</button>
```
with an active NavLink (mirroring `nav-chat` L26-29):
```razor
<NavLink id="nav-server" data-testid="nav-server" class="sidebar-nav__row" href="/server" aria-label="Server">…<span>Server</span></NavLink>
```
- **Invariant**: `nav-server` no longer carries `is-disabled` / `disabled` / `aria-disabled` and routes to `/server`.

---

## DOM hooks (stable `data-testid`)

| `data-testid` | Element | Honest state it reflects |
|---------------|---------|--------------------------|
| `server-page` | page root (`/server`) | the Server surface is active |
| `server-toggle` | start/stop control | enabled only when `IsSupported`; reflects `ServerState` |
| `server-status` | status text | `stopped` / `starting` / `running` / `stopping` / `error` (text, not color-only) |
| `server-unsupported` | honest "not supported on this platform" note | shown only when `IsSupported == false` |
| `server-pilot-light` | copper pilot-light dot | `data-state` attr = the real `ServerState`; **lit class only when `running`** |
| `server-endpoint` | bound URL display | present only when `running`; verbatim from `Urls` |
| `server-endpoint-copy` | copy-endpoint button | copies exact base URL; confirms honestly |
| `server-endpoint-empty` | honest "start failed / no endpoint" note | shown when reported start yields empty `Urls` (FR-006) |
| `server-routes` | route list | the documented OpenAI-compatible routes |
| `server-routes-documented-label` | "documented surface" label | present when not runtime-verified (FR-011) |
| `server-limitations` | limitations text block | localhost-only / no-auth / no-LAN (FR-019) |
| `server-scope-note` | "external tools only; in-app chat unaffected" | FR-021 |
| `server-busy` | "busy, try again" state | mapped from `ModelBusyException` (FR-016) |
| `server-request-log` | request log region | present **only** when activity observable |
| `server-request-log-omitted` | honest "not observable" note | present when activity unobservable (FR-023) |
| `server-error` | honest error text | names the diagnosed cause (FR-006) |

---

## Honesty invariants (DOM-assertable — Constitution III/IV)

1. **No port control** — there is **0** element matching a port field/slider/dropdown anywhere on `server-page` (FR-007, SC-002). The endpoint is read-only text from `Urls`.
2. **No fake security controls** — there is **0** auth toggle, **0** API-key/token input, **0** LAN/`0.0.0.0`/remote-bind control (FR-020, SC-002/008). Limits appear only as `server-limitations` text.
3. **Pilot-light tracks real state** — `server-pilot-light[data-state="running"]` (lit) appears **iff** `server-status` reads `running`; never lit in stopped/starting/stopping/error (FR-013, SC-006). Driven by `ServerStateMachine.PilotLightLit`.
4. **Endpoint only when running** — `server-endpoint` + `server-endpoint-copy` exist only while `running`; while stopped there is no copyable live URL (FR-008/009, US2 AC4). Empty `Urls` after a reported start ⇒ `server-endpoint-empty` + `server-error`, never a fabricated `server-endpoint` (FR-006).
5. **Copy copies the real URL** — `server-endpoint-copy` places the exact `ServerEndpoints.CopyPayload(Urls)` on the clipboard; no placeholder (FR-009, SC-003).
6. **Routes labeled honestly** — when not runtime-discoverable, `server-routes-documented-label` is present; routes are not shown as per-route runtime-verified status (FR-011).
7. **Request log honest** — exactly one of `server-request-log` (with only real observed entries) **or** `server-request-log-omitted` (honest note, zero entries) is present, per `RequestActivityProjection` (FR-022/023, SC-009). Default per research R2 is the omitted note unless a real hook is found on hardware.
8. **Scope/independence stated** — `server-scope-note` states the server is for external tools and that in-app chat does not route through it and is unaffected (FR-021, SC-005).

---

## Concurrency invariants (FR-015/016/017)

9. **Gate-coordinated** — the toggle calls `ILocalServerService.StartAsync`/`StopAsync`, which await `ReadyAsync()` and serialize via `IModelStateGate`. A conflict with an in-flight load/unload surfaces `server-busy` ("busy, try again") rather than hanging or corrupting state (mapped from `ModelBusyException`).
10. **No blocking** — the UI awaits the service and re-renders via `await InvokeAsync(StateHasChanged)`; **no** `.Result`/`.Wait()` (KI-005). Rapid toggle spamming shows honest `starting`/`stopping` transitional state and does not interleave calls.
11. **Chat unaffected** — toggling the server never interrupts an in-app chat stream; in-app chat works identically with the server stopped, starting, or running (FR-021, SC-005).

---

## Accessibility invariants (FR-014/026, SC-010 — both themes)

12. Every interactive control (`server-toggle`, `server-endpoint-copy`, any request-log control) is **labeled**, **keyboard-reachable**, and announces state changes; server state is conveyed by **text/label**, not the copper color alone.
13. The panel uses the **Foundry Copper** accent (DESIGN §3.1/§10) and meets **WCAG AA** in **Workshop Daylight** and **Night Forge**; the pilot-light ember→steady motion maps to real state (DESIGN §10 motion principle 1) and is not a free-running animation.

---

## DevFlow assertion sketch (Layer B — see quickstart.md)

```text
navigate /server
assert server-page present; nav-server not disabled
assert NO port control, NO auth/API-key/LAN control on server-page          (SC-002/008)
toggle start → assert server-status: starting → running                      (SC-001)
assert server-endpoint present, text == a real Urls value (host:port)        (SC-001)
assert server-pilot-light[data-state=running] lit                            (SC-006)
assert server-routes lists /v1/chat/completions, /v1/models                  (SC-003)
assert server-limitations + server-scope-note present                        (SC-005/008)
assert exactly one of server-request-log / server-request-log-omitted        (SC-009)
copy → assert clipboard == ServerEndpoints.CopyPayload(Urls)                  (SC-003)
[external curl proof runs here — see quickstart Layer B]                      (SC-004)
toggle stop → assert server-status: stopping → stopped; pilot-light not lit   (SC-006)
```
