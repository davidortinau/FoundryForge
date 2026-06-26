# Phase 0 Research: M5 — Local server toggle

All decisions below resolve the Technical Context unknowns. Genuine runtime unknowns (FL observable request activity; the exact runtime-served route set) are **PINNED for the Apple-Silicon hardware run** with an honest fallback already specified — never guessed (Constitution I/III).

---

## R1 — `StartAsync` → `StartWebServiceAsync` + `Urls`; no port control

**Decision**: `ILocalServerService.StartAsync(ct)` awaits `FoundryLifecycle.GetManagerTypedAsync(ct)` (the single shared `FoundryLocalManager`, `ReadyAsync()`-gated), calls `manager.StartWebServiceAsync(ct)`, then reads back `manager.Urls` and returns it as `IReadOnlyList<string>`. `StopAsync(ct)` symmetrically calls `manager.StopWebServiceAsync(ct)`. `Urls` is a property returning the **actual** bound address(es) FL chose. There is **no** port parameter on `StartWebServiceAsync`.

**Rationale**: Confirmed by FL SDK reflection (`StartWebServiceAsync(CancellationToken?)`, `StopWebServiceAsync(CancellationToken?)`, `string[] Urls`). The manager access pattern mirrors the proven `FoundryCatalogService.CatalogAsync` (`FoundryCatalogService.cs` L160-163: `await _lifecycle.GetManagerTypedAsync(ct)`), guaranteeing the one-singleton contract (FR-003). Showing `Urls` verbatim (never a hard-coded/assumed/entered port) satisfies FR-007/FR-008.

**Honesty reconciliation (recorded)**: `docs/PLAN.md` L123 reads "bind `127.0.0.1:<port>` (configurable port)." The reflection finding shows **no** port argument. Per Constitution III/IV the spec supersedes that wording: M5 ships **zero** port controls and shows the real bound URL. (Spec Clarifications reconciliation note.)

**Edge handling**: If `Urls` is **empty** after a reported start, treat it as a failed/incomplete start → transition to `Error` with an honest message; never present an empty/fabricated endpoint as live (spec Edge Cases; FR-006). Multiple URLs ⇒ present all; the copy affordance copies one real, unambiguous address (FR-008, FR-009).

**Alternatives rejected**: (a) a configurable-port UI — rejected, FL has no such API (Constitution IV dead-control ban); (b) constructing a second `FoundryLocalManager` for the server — rejected, violates the one-singleton contract (FR-003, Constitution V).

---

## R2 — Observable request activity: live log **in or out** (PINNED for hardware)

**Decision**: Model the request log behind a Core seam `RequestActivityProjection` that renders **only real observed** entries and **omits** the log (with a brief honest note) when no observable source exists. The decision of whether FL **exposes** observable per-request activity is **PINNED for the Apple-Silicon hardware run**: during the DevFlow e2e, inspect the live `FoundryLocalManager` / web-service surface for any observable request hook (event, callback, queryable counter, or log stream). 

- **If observable** ⇒ wire `RequestLog.razor` to the real source; each entry must trace to a real request (e.g. the external `curl` produces an entry), never a timer/animation (FR-022).
- **If NOT observable** (the expected default given the reflection surface shows no request hook) ⇒ `RequestLog.razor` renders an honest note ("per-request activity is not observable from Foundry Local") and **zero** fabricated entries (FR-023, SC-009).

**Rationale**: Constitution III forbids fabricated log lines; US7 is **P3 and explicitly conditional**. The milestone is complete and honest with the log omitted. The reflection surface confirmed only `StartWebServiceAsync`/`StopWebServiceAsync`/`Urls` — **no** request-activity member was observed, so the **honest-omit path is the planned default**; the hardware run gets the final say and may upgrade to a real log if a hook is found. Either branch is unit-testable (empty source ⇒ no log).

**Alternatives rejected**: a decorative "request received" animation or a timer-driven feed — rejected outright (fabrication, Constitution III); polling `Urls`/health as a fake "activity" proxy — rejected (not real per-request activity; misleading).

---

## R3 — Route-list source: documented OpenAI-compatible surface

**Decision**: `ServerEndpoints` exposes a **static documented** OpenAI-compatible route list — `/v1/chat/completions`, `/v1/models`, and `/v1/embeddings` (where applicable) — derived from the base URL in `Urls`. The UI labels it as the **documented** surface (honest framing), not per-route runtime-verified status, **unless** the exact served route set is programmatically discoverable from FL (PINNED for hardware; not assumed available).

**Rationale**: The reflection surface exposes no route-enumeration API, so per-route runtime verification cannot be claimed. FR-011/SC-003 mandate the "documented surface" honest label in that case. The `/v1/chat/completions` and `/v1/models` routes are the OpenAI-compatible contract FL implements and are exactly what the external-client proof (R6) exercises. Keeping the list as static Core data makes it unit-testable and keeps zero FL dependency in the route presentation.

**Alternatives rejected**: claiming live per-route health without a discovery API — rejected (fabricated runtime status, Constitution III); omitting routes entirely — rejected (undercuts the "wow"; the documented surface is genuinely useful and honest when labeled).

---

## R4 — Concurrency coordination with `IModelStateGate`

**Decision**: `LocalServerService.StartAsync`/`StopAsync` first `await ReadyAsync()`, then run the FL web-service call **inside** `IModelStateGate.MutateAsync(serverScopeKey, MutationPolicy.Drain, async () => { ... }, ct)` so the toggle serializes against in-flight model load/unload mutations on the shared manager. A conflicting operation either drains (default) or, where a non-blocking response is required, surfaces `ModelBusyException` mapped to an honest "server is busy with a model operation, try again" UI state. No `.Result`/`.Wait()` — fully `await`/`await foreach` (KI-005).

**Rationale**: One `FoundryLocalManager` backs both the UI load/unload path and the web service (Constitution V, PLAN L75). `ModelStateGate` (`ModelStateGate.cs`) already serializes mutations and rejects/drains against active generations via `MutationPolicy` + `ModelBusyException`; reusing it (the same instance registered `AddSingleton<IModelStateGate, ModelStateGate>()`, `MauiProgram.cs` L55) is the established mechanism — `FoundryCatalogService` routes load/unload through `_gate.MutateAsync(alias, MutationPolicy.Drain, …)` (L93/129/144). Treating server start/stop as a **mutation** of shared native state is consistent: it must not interleave with a load/unload. The busy-mapping (`ModelBusyException` → "busy, try again") is a pure, unit-testable seam over a fake gate (SC-007).

**Scope key**: Use a dedicated stable key for the server mutation (e.g. a constant like `"__server__"`) OR the loaded model id where coordination must serialize against that model's load/unload — the contract (`service-surface.md`) fixes the exact key so start/stop and load/unload contend on the same gate. Rationale recorded there; the seam test asserts serialization + busy-mapping regardless of key.

**Alternatives rejected**: a separate lock for the server — rejected (a second lock can't see the gate's in-flight mutations → race on shared native state); blocking with `.Result` to "simplify" — rejected (KI-005; deadlocks the BlazorWebView dispatcher).

---

## R5 — `IsSupported` determination

**Decision**: `LocalServerService.IsSupported` reflects the **real** platform/SDK capability: `true` on the macOS / Apple-Silicon head where the FL dylib chain is loaded and `StartWebServiceAsync` is available, `false` otherwise. The existing `StubLocalServerService` (`IsSupported => false`, Start/Stop throw) remains the honest default/non-macOS registration; the macOS head swaps to the real `LocalServerService` in `MauiProgram.cs`. The UI gates the toggle on `IsSupported` and shows an honest "not supported on this platform" state when false — never a dead/enabled toggle.

**Rationale**: Constitution IV — never present a control for a capability the running platform lacks. The DI swap (stub → real) is a one-line surgical change mirroring how the other Foundry services register concretes on the App head (`MauiProgram.cs` L51-70). Keeping the stub means the Core/Tests projects (dylib-free) still resolve an honest `IsSupported == false` without an FL dependency.

**Alternatives rejected**: hard-coding `IsSupported => true` in the real impl — rejected (must reflect actual SDK/platform availability, not an assumption); deleting the stub — rejected (Core-only tests + non-macOS targets need the honest FL-free fallback).

---

## R6 — External-client proof (the SC-004 "real" check)

**Decision**: The defining verification is an **external, out-of-process** `curl http://127.0.0.1:<port>/v1/chat/completions` (port read from the displayed `Urls`) returning a **real OpenAI-compatible response from the loaded model**, plus `curl <base>/v1/models` returning a real listing, plus a **connection-refused** check after Stop. This runs on Apple Silicon with a model loaded; it exercises the real native server (not a dylib-free unit test) and is the SC-004/SC-011 external proof.

**Rationale**: Constitution II requires "for the exposed server, an external `curl`." This is the proof M5 is real and not theater (US3). It is genuinely out-of-process (a separate `curl` process), so it cannot be faked by in-app state. Pairing it with the post-Stop connection-refused check proves the toggle reflects real server lifecycle (FR-005, SC-004).

**Alternatives rejected**: an in-process HTTP client call as the "external" proof — rejected (not genuinely external; could share state); asserting only DOM state — rejected (DOM proves the panel, not that an external tool actually reached the model).

---

## Research summary

| # | Topic | Decision | Status |
|---|-------|----------|--------|
| R1 | Start/Stop mapping + no port | `StartWebServiceAsync`/`StopWebServiceAsync` on shared manager; show verbatim `Urls`; empty `Urls` ⇒ honest error | Confirmed (reflection) |
| R2 | Observable request activity | Honest-omit by default; real log only if a hook is found on hardware | **PINNED for hardware**, fallback specified |
| R3 | Route-list source | Static documented OpenAI-compatible list, labeled as documented surface | Confirmed; runtime-discovery PINNED |
| R4 | Gate coordination | `await ReadyAsync()` + `IModelStateGate.MutateAsync(Drain)`; `ModelBusyException` → busy-state; no blocking | Confirmed (reuse M1 gate) |
| R5 | `IsSupported` | Real platform/SDK capability; stub retained as FL-free default; DI swap on macOS head | Confirmed |
| R6 | External-client proof | Out-of-process `curl` to `/v1/chat/completions` + `/v1/models`; refused after Stop | Confirmed (Constitution II) |

**All Technical Context unknowns resolved.** The two genuine runtime unknowns (R2 observable activity; R3 runtime route discovery) carry a specified honest fallback and are PINNED for the Apple-Silicon run — no fabrication, no guessing.
