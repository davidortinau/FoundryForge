# Contract: `IFoundryCatalogService` (catalog/model operations)

**Project**: `FoundryStudio.Core/Abstractions` (interface) · impl
`FoundryStudio.Foundry/FoundryCatalogService.cs`
**Satisfies**: FR-011 · SC-005 · PLAN.md lines 57, 99–101 · DEC-004

Wraps Foundry Local's catalog/model operations behind a stable, FL-free interface so M2's
catalog UI and M3's management UI code against a seam, not the SDK. Load/unload route through
`IModelStateGate` (FR-008). Maps FL `ICatalog`/`IModel` results into the FL-free `ModelInfo` /
`ModelVariant` DTOs so filtering/heuristics stay pure (research R6).

```csharp
namespace FoundryStudio.Core.Abstractions;

using FoundryStudio.Core.Models;

public interface IFoundryCatalogService
{
    Task<IReadOnlyList<ModelInfo>> BrowseAsync(CatalogFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<ModelInfo?> GetModelAsync(string alias,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelVariant>> GetVariantsAsync(string alias,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelInfo>> ListCachedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelInfo>> ListLoadedAsync(CancellationToken cancellationToken = default);

    Task DownloadAsync(string alias, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// Routes through IModelStateGate.MutateAsync (load).
    Task LoadAsync(string alias, CancellationToken cancellationToken = default);

    /// Routes through IModelStateGate.MutateAsync (unload).
    Task UnloadAsync(string alias, CancellationToken cancellationToken = default);

    /// Deletes from the local cache. PROTECTED USER DATA — requires explicit consent
    /// (Constitution IV); callers pass a consent token / confirmed flag.
    Task DeleteFromCacheAsync(string alias, bool userConfirmed,
        CancellationToken cancellationToken = default);
}
```

### Behavioral contract

| # | Given | When | Then |
|---|---|---|---|
| 1 | The ready-gated manager | `BrowseAsync` / `ListCachedAsync` / `ListLoadedAsync` | returns results sourced from FL catalog/model ops, projected to `ModelInfo` (FR-011, SC-005). |
| 2 | Load or unload via the service | it executes | routes through `IModelStateGate` and never mutates state mid-stream (FR-011/008, SC-005). |
| 3 | A model with multiple variants | `GetVariantsAsync` | exposes its variants for later UI variant selection (FR-011). |
| 4 | A cached model | `DeleteFromCacheAsync(.., userConfirmed:false)` | does **not** delete; protected user data is never removed without consent (Constitution IV). |
| 5 | Browse with a `CatalogFilter` | filter applied | filtering is the **pure** `CatalogFilter.Apply` over DTOs (testable without FL; FR-016). |

### Scope guard
M1 provides the **service**, not the M2 catalog UI / M3 management UI / memory-fit badge
(FR-019). The RAM-fit heuristic is consumed as a pure seam; its badge UI is M2.

### Test notes
Pure seams (`CatalogFilter`, `RamFitHeuristic`) are unit-tested directly (no FL). The
FL-mapping paths are exercised in the M1 service-level smoke / DevFlow end-to-end (FR-021),
since they require the manager.
