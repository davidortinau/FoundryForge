# Phase 1 Data Model: M5 — Local server toggle

All entities below live in `FoundryForge.Core` (FL-free, dylib-free) **except** the existing `ILocalServerService` seam (implemented in `.Foundry` by `LocalServerService`) and the FL-owned `FoundryLocalManager`. No persistent store is introduced — every entity is in-memory/display-only and reflects real service/SDK state (Constitution III/IV).

---

## `ServerState` (enum) — the honest lifecycle

`src/FoundryForge.Core/Server/ServerState.cs`

```csharp
public enum ServerState
{
    Stopped,    // not running; the honest default at launch (FR-018)
    Starting,   // StartAsync in flight (await ReadyAsync + StartWebServiceAsync)
    Running,    // StartWebServiceAsync succeeded AND Urls is non-empty
    Stopping,   // StopAsync in flight
    Error,      // start/stop failed, or Urls empty after a reported start (FR-006)
}
```

- **Derived from the real service lifecycle**, never fabricated (FR-004).
- The copper pilot-light is **steady only** in `Running`; off in `Stopped`; an honest transitional indicator in `Starting`/`Stopping`; never lit otherwise (FR-013, SC-006).

---

## `ServerStatus` (record) — display projection

`src/FoundryForge.Core/Server/ServerStatus.cs`

```csharp
public sealed record ServerStatus(
    ServerState State,
    IReadOnlyList<string> Urls,   // verbatim from ILocalServerService.Urls; empty unless Running
    string? Message = null)       // honest diagnosed cause for Error/Busy; null otherwise
{
    public static ServerStatus Stopped { get; } = new(ServerState.Stopped, Array.Empty<string>());
    public bool IsRunning => State == ServerState.Running;
    public bool IsBusy    => State is ServerState.Starting or ServerState.Stopping;
}
```

| Field | Meaning | Honesty rule |
|-------|---------|--------------|
| `State` | current lifecycle state | mapped from real service; never faked |
| `Urls` | the **actual** bound address(es) from `ILocalServerService.Urls` | non-empty only when `Running`; empty after a reported start ⇒ caller sets `Error` (R1) |
| `Message` | diagnosed cause (error) or "busy, try again" | names the real cause; `null` when none |

---

## `ServerStateMachine` — pure transition validator

`src/FoundryForge.Core/Server/ServerStateMachine.cs`

Validates the legal lifecycle transitions; the UI/service use it to reject illegal interleavings and to map results to `ServerState`.

```text
Stopped  → Starting              (user activates start)
Starting → Running               (StartWebServiceAsync ok AND Urls non-empty)
Starting → Error                 (start failed OR Urls empty)           (FR-006, R1)
Running  → Stopping              (user activates stop)
Stopping → Stopped               (StopWebServiceAsync ok)
Stopping → Error                 (stop failed)
any      → Error                 (native fault)
Error    → Starting | Stopped    (retry / acknowledge)
```

- Pure, deterministic, dylib-free. **Invariant under test**: `PilotLightLit(state) == (state == Running)` — the pilot-light is never lit outside `Running` (SC-006).

---

## `ServerEndpoints` — endpoint + route presentation

`src/FoundryForge.Core/Server/ServerEndpoints.cs`

Derives the copy-friendly base URL and the documented OpenAI-compatible route list from the real `Urls` (R3). No FL dependency.

| Concept | Shape | Rule |
|---------|-------|------|
| `BaseUrl(urls)` | first/primary real address from `Urls`, normalized (no trailing slash) | from `Urls` only; never hard-coded; throws/empty-result when `urls` empty (no live endpoint while stopped — FR-009/US2 AC4) |
| `AllBaseUrls(urls)` | all real addresses when `Urls` has multiple | present all (FR-008) |
| `CopyPayload(urls)` | the exact base URL string to place on the clipboard | exact, from `Urls`; never a placeholder (FR-009, SC-003) |
| `DocumentedRoutes` | static `IReadOnlyList<ServerRoute>` | the documented OpenAI-compatible surface (R3) |
| `RouteUrl(baseUrl, route)` | `baseUrl + route.Path` | for display/copy of a full route URL |

### `ServerRoute` (record) — a documented route entry

```csharp
public sealed record ServerRoute(string Path, string Description);
// DocumentedRoutes (static):
//   /v1/chat/completions  — chat completions (OpenAI-compatible)
//   /v1/models            — list available models
//   /v1/embeddings        — embeddings (where applicable)
```

- Labeled in the UI as the **documented** OpenAI-compatible surface, **not** per-route runtime-verified status, unless FL exposes a discovery API (R3, FR-011, SC-003).

---

## `ServerLimitations` — informational facts (never controls)

`src/FoundryForge.Core/Server/ServerLimitations.cs`

Static data describing what the FL server **is and is not** — rendered as plain text, never as toggles/fields (FR-019/020, Constitution IV).

```csharp
public static class ServerLimitations
{
    // each is an informational fact string for display:
    public const string LocalhostOnly = "Localhost only — binds 127.0.0.1, not reachable from the LAN.";
    public const string NoAuth        = "No authentication — any local process can call it.";
    public const string NoLanBind     = "No LAN / 0.0.0.0 binding — not exposed to other machines.";
    public const string ExternalOnly  = "For external tools only — in-app chat does not use this server and is unaffected by it.";
    public static IReadOnlyList<string> All { get; } // the four facts, ordered
}
```

- **Invariant under test**: these are **data**, and the DOM contract (`server-ui.dom.md`) asserts there is **0** auth/API-key/LAN-bind control anywhere (SC-002/008).

---

## `RequestActivity` (conditional) + `RequestActivityProjection`

`src/FoundryForge.Core/Server/RequestActivityProjection.cs`

The "render only observed activity, else omit" decision seam (R2). The activity **source** is abstracted so the projection is unit-testable without FL.

```csharp
public sealed record RequestActivityEntry(DateTimeOffset At, string Summary); // a REAL observed request

public static class RequestActivityProjection
{
    // observed == null  ⇒ FL exposes no observable activity ⇒ omit the log (with honest note)
    // observed empty     ⇒ log region present but no entries (server running, no traffic yet)
    // observed non-empty ⇒ render only these real entries
    public static RequestLogView Project(IReadOnlyList<RequestActivityEntry>? observed);
}

public sealed record RequestLogView(bool Show, string? HonestNote, IReadOnlyList<RequestActivityEntry> Entries);
```

| Input | `Show` | `HonestNote` | `Entries` | Rule |
|-------|--------|--------------|-----------|------|
| `null` (unobservable) | `false` | "Per-request activity is not observable from Foundry Local." | empty | FR-023, SC-009 |
| empty list | `true` | null | empty | running, no traffic yet |
| non-empty | `true` | null | the real entries | only real observed (FR-022) |

- **Invariant under test**: `Project(null).Entries` and `Project(empty).Entries` contain **zero** fabricated rows; no timer/animation is ever a source (SC-009).

---

## Existing entities consumed (unchanged)

| Entity | Source | Role in M5 |
|--------|--------|-----------|
| `ILocalServerService` | `Core/Abstractions/ILocalServerService.cs` | the UI's only gateway: `IsSupported`, `Urls`, `StartAsync`, `StopAsync` |
| `FoundryLocalManager` | FL SDK (behind `.Foundry`) | the **one** singleton; `StartWebServiceAsync`/`StopWebServiceAsync`/`Urls` |
| `IFoundryLifecycle` / `FoundryLifecycle` | `Core/Abstractions` + `.Foundry` | `ReadyAsync()` gate; `GetManagerTypedAsync()` for the shared manager |
| `IModelStateGate` / `ModelStateGate` | `Core/Abstractions` + `Core/Concurrency` | serialize server start/stop vs load/unload; `MutationPolicy.Drain`, `ModelBusyException` |
| `StubLocalServerService` | `Core/PostV1` | retained honest FL-free default (`IsSupported == false`) for non-macOS/Tests |

**No schema changes** to any existing entity. **No persistent store.** No model-cache, settings, or chat-history mutation (Constitution IV — nothing to consent-gate because nothing is destroyed).
