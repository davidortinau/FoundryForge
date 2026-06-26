# Contract: `IFoundryCatalogService` read surface (M2 usage)

**Status**: existing interface (M1) — **no signature change**. M2 enriches the *returned data* and constrains the *UI to read-only methods only*. This contract pins what M2 may call and what each call now guarantees.

`src/FoundryStudio.Core/Abstractions/IFoundryCatalogService.cs` (FL-free seam, DEC-004 / Constitution V).

## Methods the M2 catalog UI MAY call (read-only — R7)

```csharp
Task<IReadOnlyList<ModelInfo>> BrowseAsync(CatalogFilter? filter = null, CancellationToken ct = default);
Task<ModelInfo?>               GetModelAsync(string alias, CancellationToken ct = default);
Task<IReadOnlyList<ModelVariant>> GetVariantsAsync(string alias, CancellationToken ct = default);
Task<IReadOnlyList<ModelInfo>> ListCachedAsync(CancellationToken ct = default);
Task<IReadOnlyList<ModelInfo>> ListLoadedAsync(CancellationToken ct = default);
```

## Methods the M2 UI MUST NOT call (mutation — M3/M4 surface, FR-013/SC-008)

```
DownloadAsync · LoadAsync · UnloadAsync · DeleteFromCacheAsync   // present on the seam, NOT invoked by M2 UI
```

## Behavioral guarantees added in M2 (enrichment)

| Method | M1 behavior | M2 contract |
|--------|-------------|-------------|
| `BrowseAsync` | returns `MapBasic` stubs (`SizeGb:0`, `Device.Gpu`, empty Task/Provider/Variants) | returns **enriched** `ModelInfo` per `research.md` R1; honest nulls where FL omits; `IsCached`/`IsLoaded` accurate; never mutates FL state |
| `GetModelAsync` | basic stub | enriched single model or `null` if alias not found |
| `GetVariantsAsync` | `Array.Empty` stub (`TODO(M2)`) | real variants mapped from `IModel.Variants` (and/or `GetModelVariantAsync`); empty list = FL reports none |
| `ListCachedAsync` | basic, `IsCached:true` | enriched, `IsCached:true` |
| `ListLoadedAsync` | basic, `IsLoaded:true` | enriched, `IsLoaded:true` |

## Invariants
- **No mutation on read**: none of the five read methods trigger an FL `DownloadAsync`/`LoadAsync` (SC-008). Cross-checked by confirming a non-cached model stays non-cached after a full browse/search/filter session.
- **Seam purity**: returns Core DTOs only; no FL type crosses the boundary (Constitution V).
- **Honesty**: every nullable field reflects real FL absence; no fabricated/default stand-in (FR-012).
- **Error surfacing**: on FL/catalog failure the method throws a diagnosable exception (caught by the App into the honest `Error` state, FR-016 — never silently returns empty).

## Error contract (FR-016)
A fetch failure (FL unavailable / catalog error) surfaces as a thrown exception carrying the diagnosed cause. The App layer maps it to `CatalogViewState.Status = Error` with `ErrorMessage` = the cause and a retry that re-invokes `BrowseAsync`. A failure is **never** represented as an empty (zero-model) success.
