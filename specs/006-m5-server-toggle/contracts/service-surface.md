# Contract: Service surface — `LocalServerService` (the only new FL-bound piece)

M5 implements the existing seam `ILocalServerService` for real in the **Foundry layer**. This is the **one** new FL-bound class; everything else is FL-free Core/UI. It follows the established `FoundryCatalogService` pattern (manager via `FoundryLifecycle.GetManagerTypedAsync`, mutations via `IModelStateGate`).

---

## Existing seam (unchanged) — `ILocalServerService`

`src/FoundryStudio.Core/Abstractions/ILocalServerService.cs`

```csharp
public interface ILocalServerService
{
    bool IsSupported { get; }
    IReadOnlyList<string> Urls { get; }
    Task<IReadOnlyList<string>> StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

The UI consumes **only** this (plus the Core seams + M1 lifecycle/gate). It never references FL types (FR-024, Constitution V).

---

## New impl — `LocalServerService`

`src/FoundryStudio.Foundry/LocalServerService.cs`

```csharp
public sealed class LocalServerService : ILocalServerService
{
    public LocalServerService(FoundryLifecycle lifecycle, IModelStateGate gate, ILogger<LocalServerService> logger);

    public bool IsSupported { get; }                 // real platform/SDK capability (R5)
    public IReadOnlyList<string> Urls { get; }       // last-read real Urls; empty unless running

    public Task<IReadOnlyList<string>> StartAsync(CancellationToken ct = default);
    public Task StopAsync(CancellationToken ct = default);
}
```

### `IsSupported` (R5, FR-002)
- Reflects the **real** platform/SDK capability (the macOS / Apple-Silicon head where FL's web service is available). Not a hard-coded `true`. When false, the UI shows an honest "not supported" state and disables the toggle (no dead/enabled control).

### `StartAsync` (FR-002/003/005/006/015/017)
1. If `!IsSupported` ⇒ throw/return an honest unsupported result (UI maps to honest state).
2. `await lifecycle.ReadyAsync(ct)` — gate on readiness; **no** `.Result`/`.Wait()` (KI-005).
3. Obtain the **shared** manager: `var manager = await lifecycle.GetManagerTypedAsync(ct);` — the **one** `FoundryLocalManager`; **never** construct a second (FR-003). (Mirrors `FoundryCatalogService.cs` L160-163.)
4. Coordinate with the gate so the toggle does not race an in-flight load/unload on shared native state (R4):
   ```csharp
   await gate.MutateAsync(ServerScopeKey, MutationPolicy.Drain, async () =>
   {
       await manager.StartWebServiceAsync(ct).ConfigureAwait(false);
   }, ct).ConfigureAwait(false);
   ```
5. Read back `Urls = manager.Urls` (the **actual** bound address(es)). If `Urls` is **empty** ⇒ treat as a failed/incomplete start: surface an honest error, leave state `Error`, do **not** present a fabricated endpoint (R1, FR-006).
6. Return the real `Urls`.

### `StopAsync` (FR-005/015/017/018)
1. `await lifecycle.ReadyAsync(ct)`.
2. `var manager = await lifecycle.GetManagerTypedAsync(ct);`
3. ```csharp
   await gate.MutateAsync(ServerScopeKey, MutationPolicy.Drain, async () =>
   {
       await manager.StopWebServiceAsync(ct).ConfigureAwait(false);
   }, ct).ConfigureAwait(false);
   ```
4. Clear `Urls` (server is down; no live endpoint).

### `ServerScopeKey`
- A stable constant (e.g. `"__server__"`) OR the loaded model id where the server toggle must serialize against that model's load/unload on the shared manager. The chosen key is fixed here so server start/stop and model load/unload contend on the **same** `IModelStateGate` instance (`MauiProgram.cs` L55 — one singleton). The busy path maps `ModelBusyException` → an honest "server is busy with a model operation, try again" surfaced by the UI (FR-016).

### Concurrency & honesty invariants
- **Single manager**: only `lifecycle.GetManagerTypedAsync()` — no `new FoundryLocalManager(...)` anywhere (FR-003, SC-011).
- **No blocking**: fully `await`; the `NoBlockingInitGuard` test stays green (KI-005, FR-017).
- **Real values only**: `Urls` is always the verbatim SDK value; never synthesized (FR-008/025).
- **Clean shutdown**: app-exit disposal stops the web service via the lifecycle so no orphaned listener survives; next launch starts `Stopped` (FR-018).

---

## DI registration (one-line swap)

`src/FoundryStudio.App/MauiProgram.cs` (in `RegisterFoundryStudioServices`)

```diff
- // Post-v1 honest stubs keep the DI graph stable for M5/M6 (IsSupported == false; operations throw).
- services.AddSingleton<ILocalServerService, StubLocalServerService>();
+ // M5: the real exposed server over the single shared FoundryLocalManager (Foundry layer; the only new FL-bound piece).
+ services.AddSingleton<ILocalServerService, LocalServerService>();
```

- `LocalServerService` resolves `FoundryLifecycle` (concrete, for `GetManagerTypedAsync`) and `IModelStateGate` — both already registered as singletons (`MauiProgram.cs` L51-55), guaranteeing the one-manager/one-gate contract.
- `StubLocalServerService` is **retained** in `Core/PostV1` as the honest FL-free default for non-macOS targets and the Core-only Tests project (`IsSupported == false`).
- The other PostV1 stubs (`IEmbeddingService`, `ITranscriptionService`) are untouched.

---

## What this contract forbids (Constitution III/IV/V)

- ❌ A second `FoundryLocalManager` (FR-003).
- ❌ Any FL type referenced from the UI layer (FR-024).
- ❌ A port argument to `StartWebServiceAsync` (none exists — R1).
- ❌ `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` in the server path (KI-005, FR-017).
- ❌ Presenting an empty `Urls` as a live endpoint (FR-006).
- ❌ A separate lock that bypasses the shared `IModelStateGate` (R4).
