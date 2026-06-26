# Contract: Service surface relied on by M3 (+ the one additive delta)

M3 is overwhelmingly UI + pure seams. It relies on the **existing** `IFoundryCatalogService` and `ISettingsService` surface and introduces exactly **one** back-compatible, additive change: variant targeting (research R7). The UI consumes **only** these abstractions + Core seams — never the FL SDK (FR-031, Constitution V / DEC-004).

---

## `IFoundryCatalogService` (`src/FoundryStudio.Core/Abstractions/IFoundryCatalogService.cs`)

### Relied on unchanged
| Method | M3 use | Story/FR |
|--------|--------|----------|
| `BrowseAsync(filter?, ct)` | full model universe for grouping (no `CachedOnly`) | US4 / FR-016 |
| `ListCachedAsync(ct)` | **authoritative** cached set for grouping (KI-009 — no `CachedOnly` re-filter) | US4 / FR-017 |
| `ListLoadedAsync(ct)` | authoritative loaded set → app-wide indicator + per-card badge (refresh-on-mutation) | US2 / FR-007 |
| `GetVariantsAsync(alias, ct)` | variant list for `VariantSelectionState` | US5 / FR-019 |
| `UnloadAsync(alias, ct)` | gate-serialized unload | US2 / FR-006 |
| `DeleteFromCacheAsync(alias, userConfirmed, ct)` | consent-gated delete (throws on `false`) — the enforcement point | US3 / FR-010..015 |

### Additive delta (back-compatible — R7)
```csharp
// BEFORE
Task DownloadAsync(string alias, IProgress<double>? progress = null, CancellationToken ct = default);
Task LoadAsync(string alias, CancellationToken ct = default);

// AFTER (optional variantId; all existing callers compile unchanged)
Task DownloadAsync(string alias, IProgress<double>? progress = null, string? variantId = null, CancellationToken ct = default);
Task LoadAsync(string alias, string? variantId = null, CancellationToken ct = default);
```
**Contract for the delta**
- `variantId == null` ⇒ identical to today's behavior (default variant). Existing M1/M2 callers and `LoadAsync` internals are unaffected.
- `variantId != null` ⇒ in `FoundryCatalogService`, resolve the matching `IModel` from `model.Variants` (by `Info.Id`) and call `IModel.SelectVariant(variant)` **before** `DownloadAsync`/`LoadAsync` — so the pinned variant is honored (FR-020).
- An unknown `variantId` ⇒ honest failure (no silent fallback to default that would mislead) — surfaced to the UI.
- `LoadAsync` retains its existing Drain gating and "download-if-not-cached then load-if-not-loaded" behavior (`FoundryCatalogService.cs` L89-106).
- **No new method, no new interface, no layer breach** — Constitution V intact; no Complexity Tracking entry (Phase-1 re-check PASS).

---

## `ISettingsService` (`src/FoundryStudio.Core/Abstractions/ISettingsService.cs`) — unchanged

| Method | M3 use | FR |
|--------|--------|----|
| `GetAsync(ct)` | read current `AppSettings.ModelCacheDirectory` + `DefaultModel` | US7 / FR-025 |
| `UpdateAsync(settings, ct)` | persist `settings with { ModelCacheDirectory = newDir }` — **pointer only**, never moves/wipes cache files | US7 / FR-026 |
| `ResetAsync(userConfirmed, ct)` | (not required by M3; remains consent-gated) | — |

**Contract for cache-dir change (US7)**
- Save validates the new directory (exists/writable); invalid ⇒ honest error, previous value retained (FR-027).
- A warn/confirm (`ConfirmDialog`) precedes persistence, stating "existing cached models under the old directory may not appear; nothing is moved or deleted" (FR-026).
- `FileSettingsService.UpdateAsync` already persists without touching the model cache — no code change needed there for the no-wipe guarantee.
- Edge case: if the deleted/relocated model equals `AppSettings.DefaultModel`, leave `DefaultModel` honest (cleared/flagged), never dangling.

---

## Foundry-layer changes (`src/FoundryStudio.Foundry/FoundryCatalogService.cs`)

| Change | Detail | FR |
|--------|--------|----|
| KI-009 cached-source trust | grouping uses `ListCachedAsync` (already sources `GetCachedModelsAsync` w/o `CachedOnly`); verify FL `Info.Cached` semantics on hardware | FR-017 |
| Variant targeting | when `variantId` provided, `SelectVariant` before download/load | FR-020 |
| Cancellation pass-through | already passes `ct` to `model.DownloadAsync` (L86) — verify FL honors it (research U1) | FR-003 |
| Download progress | already wired (`p => progress?.Report(p)`, L86) — marshal to UI via `InvokeAsync` in the App (KI-005) | FR-002 |

---

## Layering invariant (FR-031, Constitution V)
- `FoundryStudio.App` references only `IFoundryCatalogService`, `ISettingsService`, and `FoundryStudio.Core` seams. **No** `using Microsoft.AI.Foundry.Local` anywhere in `.App`.
- All FL types (`IModel`, `ICatalog`, `SelectVariant`) stay inside `FoundryStudio.Foundry`.
- All model mutations route through the single `IModelStateGate` (one manager; drain/reject; `ModelBusyException` surfaced honestly).
