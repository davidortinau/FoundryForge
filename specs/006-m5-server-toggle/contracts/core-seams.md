# Contract: Core pure-logic seams (new in M5)

All seams below live in `FoundryStudio.Core/Server/`, are **FL-free and dylib-free**, and are unit-tested in `tests/FoundryStudio.Tests` without a native Foundry Local dylib (FR-027, SC-001/002/003/006/007/008/009). They mirror the M1–M4 precedent (`ModelStateGate`, `RamFitHeuristic`). **No new package references** — plain managed code + static data.

---

## `ServerState` / `ServerStatus` — honest lifecycle (US1, US4)

`src/FoundryStudio.Core/Server/ServerState.cs`, `ServerStatus.cs`

```csharp
public enum ServerState { Stopped, Starting, Running, Stopping, Error }

public sealed record ServerStatus(
    ServerState State, IReadOnlyList<string> Urls, string? Message = null)
{
    public static ServerStatus Stopped { get; }
    public bool IsRunning { get; } // State == Running
    public bool IsBusy { get; }    // Starting or Stopping
}
```

**Contract**
- `Urls` is **non-empty only** when `State == Running`; an `Error`/`Stopped` status carries `Array.Empty<string>()`.
- `Message` names the real diagnosed cause for `Error`/busy; `null` otherwise (FR-004/006).

---

## `ServerStateMachine` — transition validator (US1, US4)

`src/FoundryStudio.Core/Server/ServerStateMachine.cs`

```csharp
public static class ServerStateMachine
{
    public static bool CanTransition(ServerState from, ServerState to);
    public static bool PilotLightLit(ServerState state); // == (state == Running)
}
```

**Contract**
- Legal edges only (data-model.md table): `Stopped→Starting`, `Starting→Running|Error`, `Running→Stopping`, `Stopping→Stopped|Error`, `any→Error`, `Error→Starting|Stopped`. Illegal edges return `false`.
- `PilotLightLit(s)` is `true` **iff** `s == Running` (FR-013, SC-006).
- Pure, deterministic, dylib-free.

**Tests** (`ServerStateMachineTests`, SC-006/007): each legal edge `true`; a sampling of illegal edges `false`; `PilotLightLit` true only for `Running`, false for the other four states.

---

## `ServerEndpoints` / `ServerRoute` — endpoint + routes (US1, US2)

`src/FoundryStudio.Core/Server/ServerEndpoints.cs`

```csharp
public sealed record ServerRoute(string Path, string Description);

public static class ServerEndpoints
{
    public static IReadOnlyList<ServerRoute> DocumentedRoutes { get; } // /v1/chat/completions, /v1/models, /v1/embeddings
    public static string? BaseUrl(IReadOnlyList<string> urls);          // primary real address, normalized; null if empty
    public static IReadOnlyList<string> AllBaseUrls(IReadOnlyList<string> urls);
    public static string? CopyPayload(IReadOnlyList<string> urls);      // exact base URL; null if empty
    public static string RouteUrl(string baseUrl, ServerRoute route);  // baseUrl + route.Path
}
```

**Contract**
- `BaseUrl`/`CopyPayload` return the **exact** address from `urls` (trailing slash normalized), never a hard-coded/assumed value; **`null`** when `urls` is empty (no live/copyable endpoint while stopped — FR-008/009, US2 AC4).
- `AllBaseUrls` returns every real address when `urls` has multiple (FR-008).
- `DocumentedRoutes` is the static documented OpenAI-compatible surface (R3) — the UI labels it as documented, not runtime-verified (FR-011).
- Pure, dylib-free.

**Tests** (`ServerEndpointsTests`, SC-001/003): single URL → exact base + copy payload; multiple URLs → all presented; **empty URLs → `BaseUrl`/`CopyPayload` null** (no live endpoint); `DocumentedRoutes` contains `/v1/chat/completions` and `/v1/models`; `RouteUrl` concatenates correctly with no double slash.

---

## `ServerLimitations` — informational facts (US6)

`src/FoundryStudio.Core/Server/ServerLimitations.cs`

```csharp
public static class ServerLimitations
{
    public const string LocalhostOnly = "...";
    public const string NoAuth = "...";
    public const string NoLanBind = "...";
    public const string ExternalOnly = "...";
    public static IReadOnlyList<string> All { get; } // ordered four facts
}
```

**Contract**
- Pure **data**: facts describing localhost-only / no-auth / no-LAN / external-only (FR-019/021). They are rendered as text; the seam never exposes a setter or a control. The DOM contract asserts zero matching capability controls (FR-020).

**Tests** (`ServerLimitationsTests`, SC-002/008): `All` contains the four facts including localhost-only, no-auth, no-LAN, and "external tools only / in-app chat unaffected".

---

## `RequestActivityProjection` — render-only-observed-else-omit (US7)

`src/FoundryStudio.Core/Server/RequestActivityProjection.cs`

```csharp
public sealed record RequestActivityEntry(DateTimeOffset At, string Summary);
public sealed record RequestLogView(bool Show, string? HonestNote, IReadOnlyList<RequestActivityEntry> Entries);

public static class RequestActivityProjection
{
    public static RequestLogView Project(IReadOnlyList<RequestActivityEntry>? observed);
}
```

**Contract** (R2, FR-022/023)
- `observed == null` (FL exposes no observable activity) ⇒ `Show == false`, `HonestNote` set, `Entries` empty.
- `observed` empty ⇒ `Show == true`, `HonestNote == null`, `Entries` empty (running, no traffic yet).
- `observed` non-empty ⇒ `Show == true`, `Entries` are exactly those **real** entries (no synthesis, no reordering, no timer source).
- Pure, dylib-free.

**Tests** (`RequestActivityProjectionTests`, SC-009): `Project(null)` ⇒ not shown, honest note, zero entries; `Project(empty)` ⇒ shown, zero entries; `Project([e1,e2])` ⇒ shown, exactly `[e1,e2]`. No code path fabricates an entry.

---

## Honesty invariants enforced by these seams

| Seam | Constitution III/IV invariant |
|------|-------------------------------|
| `ServerEndpoints.BaseUrl/CopyPayload` | empty `Urls` ⇒ `null` (no fabricated endpoint; no copyable "live" URL while stopped) |
| `ServerStateMachine.PilotLightLit` | lit **only** in `Running` (no fabricated "lit" state) |
| `ServerLimitations` | limits are data/text — **no** control surface for capabilities FL lacks |
| `RequestActivityProjection` | unobservable ⇒ omit with honest note; **zero** fabricated entries |
| `ServerStatus.Urls` | populated only when `Running` (real addresses only) |
