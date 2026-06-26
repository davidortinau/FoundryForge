# Contract: `IFoundryLifecycle` (ready-gate, singleton lifecycle)

**Project**: `FoundryStudio.Core/Abstractions` (interface) · impl
`FoundryStudio.Foundry/FoundryLifecycle.cs`
**Satisfies**: FR-003, FR-004, FR-005, FR-006, FR-007 · SC-001, SC-002, SC-003 · KI-005 ·
Constitution V · PLAN.md line 76

The single seam that owns Foundry Local initialization, the awaitable ready-gate, and disposal.
Every service/component awaits this before touching Foundry Local.

```csharp
namespace FoundryStudio.Core.Abstractions;

/// One Foundry Local lifecycle, shared by all consumers (the in-process UI path and the
/// future exposed server). Init runs off the BlazorWebView dispatcher; the gate is awaited,
/// never blocked on (no .Result/.Wait()).
public interface IFoundryLifecycle : IAsyncDisposable
{
    /// Awaited by every consumer before using Foundry Local. Completes exactly once on a
    /// SUCCESSFUL initialization. A failed init faults this task and NEVER marks ready.
    Task ReadyAsync(CancellationToken cancellationToken = default);

    /// Returns the one shared initialized FoundryLocalManager (awaits ReadyAsync internally).
    /// The return type is the FL SDK manager; callers in Core take it via this Foundry-layer
    /// interface only — Core itself never references the FL SDK.
    Task<object> GetManagerAsync(CancellationToken cancellationToken = default);

    /// Current lifecycle phase for the app-level "initializing" guard.
    FoundryReadyState State { get; }
}

public enum FoundryReadyState { Uninitialized, Initializing, Ready, Failed }
```

> Note: `GetManagerAsync` returns `object` in the **Core** abstraction so Core stays FL-free;
> the `FoundryStudio.Foundry` implementation exposes a strongly-typed
> `Task<FoundryLocalManager>` overload that FL-bound services consume. This keeps the test seam
> dylib-free (research R7).

### Behavioral contract

| # | Given | When | Then |
|---|---|---|---|
| 1 | Clean app start | first consumer awaits `ReadyAsync` | init runs **off** the dispatcher (`Task.Run`), `State == Initializing`, not-yet-ready surfaces stay blocked (FR-005, SC-001). |
| 2 | Init completes successfully | any consumer awaits the gate | it observes the **one** shared manager; `State == Ready`; UI updates marshalled via `InvokeAsync(StateHasChanged)` (FR-003/004, SC-002). |
| 3 | Multiple consumers await before init finishes | init finishes | init ran **exactly once**; all observe the same instance; no second manager (FR-003, SC-002). |
| 4 | Consumer awaits **after** init already completed | — | returns the already-initialized instance immediately, no re-init (edge case). |
| 5 | Init throws / is slow | gate awaited | gate faults/stays pending with an **honest diagnosed cause**; `State == Failed`/`Initializing`; never satisfied by a failed init; surfaces stay blocked (edge case, SC-001). |
| 6 | App exits | shutdown | `DisposeAsync` disposes the manager cleanly; **no synchronous blocking** on an in-flight init task (FR-007, SC-003, edge case). |
| 7 | Anywhere in the codebase | inspected for blocking | **zero** `.Result`/`.Wait()` on the init task (FR-006, SC-003). |

### Test notes
Behavior is verified at the service level (Story 1 independent test) and proven end-to-end on
real Apple Silicon at M1 close (launch → initializing → ready without deadlock; FR-021).
