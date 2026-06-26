# Phase 0 Research: M3 — Model Install & Management

All decisions below are grounded in real code, the reflected FL SDK surface, and the milestone references (Constitution I). Open **runtime** unknowns that can only be resolved on Apple-Silicon hardware are flagged explicitly and pinned for the DevFlow e2e — they are not guessed.

---

## R1 — FL `IModel.DownloadAsync` cancellation support

**Question**: Does `IModel.DownloadAsync` accept a `CancellationToken`, and does FL honor it cleanly (US1 / FR-003)?

**Evidence (reflected from `Microsoft.AI.Foundry.Local` 1.2.3, `lib/net9.0`)**:
```
DownloadAsync(Action`1 downloadProgress, Nullable`1 ct) -> Task
IsCachedAsync(Nullable`1 ct) -> Task ; IsLoadedAsync(Nullable`1 ct) -> Task
LoadAsync(Nullable`1 ct) ; UnloadAsync(Nullable`1 ct) ; RemoveFromCacheAsync(Nullable`1 ct)
GetPathAsync(Nullable`1 ct) ; SelectVariant(IModel variant) -> Void ; get_Variants() -> IReadOnlyList`1
```
The progress callback is `Action<float>` (0..1 or 0..100 — confirm scale on hardware, see R3 note). `FoundryCatalogService.DownloadAsync` (L82-87) already passes the `CancellationToken` through: `model.DownloadAsync(p => progress?.Report(p), cancellationToken)`.

**Decision**: The signature **accepts** a `CancellationToken`, so cancel is wired through the existing seam — no service change needed for the token. Whether FL **honors** mid-download cancellation cleanly (stops the transfer, leaves no half-cached model presented as ready) is a **runtime behavior unknown** at the pinned version.

- **Rationale**: We must not fabricate certainty about upstream behavior (Constitution I/IV).
- **Design that is correct regardless of FL's honoring**: (a) the UI cancels via `CancellationTokenSource.Cancel()`; (b) on `OperationCanceledException` OR completion-after-cancel, the card recomputes cached state from the **authoritative** source (`IsCachedAsync` / `ListCachedAsync`) rather than trusting an optimistic flag — so a partially-downloaded model is shown by its real cached state, never as "ready" (edge case: cancel-races-completion → single coherent state); (c) if FL ignores the token and runs to completion, the model simply appears cached (honest) and the user may delete it via the consent flow.
- **PIN for DevFlow e2e**: on a small test model, start download → Cancel mid-flight → assert via DOM the card returns to not-cached/idle and `ListCachedAsync` does not contain it. Record the observed FL cancel behavior in the M3 `Verified:` note and, if FL ignores the token, file/annotate a KNOWN-ISSUES entry (workaround = post-cancel cleanup via authoritative state recheck).

**Alternatives considered**: Polling-based pseudo-cancel (rejected — the token already exists; fabricating a cancel UI that doesn't call the token would violate capability honesty). Deleting partial files ourselves (rejected — FL owns the cache layout; we re-read authoritative cached state instead of touching FL's files).

---

## R2 — Download progress marshaling (KI-005 discipline)

**Question**: How does real percent reach the UI without starving the BlazorWebView dispatcher?

**Evidence**: KI-005 — blocking FL work on the WebView dispatcher froze first render in M0d; the rule is to offload via `Task.Run` and marshal UI updates with `await InvokeAsync(StateHasChanged)`. `Home.razor` already uses `InvokeAsync(StateHasChanged)` for catalog refresh.

**Decision**: Drive `DownloadAsync` from a background task (not the dispatcher). The `IProgress<double>` callback updates the per-model `ModelOperationState`, and each progress tick calls `await InvokeAsync(StateHasChanged)` to re-render. Throttle re-renders (e.g. only when the rounded percent changes) to avoid flooding the dispatcher. When the FL callback never fires (progress source missing — edge case), render an honest **indeterminate/“working”** state, never a fabricated advancing bar (FR-002).

**Rationale**: Real source only + off-dispatcher = satisfies FR-002 and KI-005. **Alternative rejected**: a CSS-only animated bar (violates FR-002 / capability honesty).

---

## R3 — Disk-space check (US6 / FR-022..024)

**Question**: How to warn (not block) before a download that likely won't fit, with honest "unknown"?

**Decision**: Add a pure `DiskFitHeuristic` in `FoundryStudio.Core/Catalog/` mirroring the existing `RamFitHeuristic`. It is a **pure function over numbers** — no I/O in Core (keeps it dylib-free and unit-testable, FR-034):
```csharp
public static DiskFitResult Evaluate(double? modelSizeGb, double freeDiskGb);
// null modelSizeGb            -> DiskFit.Unknown   (honest "size unknown — can't check fit", FR-024)
// size*margin <= freeDiskGb   -> DiskFit.Fits      (no warning, FR-022)
// size*margin >  freeDiskGb   -> DiskFit.Warn      (non-blocking warning, FR-022/023)
```
Documented safety margin (e.g. weights × 1.1 + a small fixed headroom) recorded in code XML-doc, consistent with the RamFit "wide margin, honest, never a confident green" philosophy. The **free-disk figure** is obtained in the App (or Foundry) layer via `System.IO.DriveInfo` on the volume of `AppSettings.ModelCacheDirectory` (`new DriveInfo(Path.GetPathRoot(cacheDir)).AvailableFreeSpace` → GB), then passed into the pure heuristic. The check **warns and never blocks** — the download button stays enabled (FR-023).

**Rationale**: `DriveInfo.AvailableFreeSpace` is the standard cross-checked way to read free space for a path's volume; keeping the numeric verdict pure preserves the dylib-free seam and matches the M2 RamFit precedent. **Alternatives rejected**: blocking the download (violates warn-not-block FR-023); guessing a fit when size is null (violates FR-024).

**Progress-scale note**: confirm on hardware whether the `Action<float>` reports 0..1 or 0..100; the `IProgress<double>` mapping normalizes to a percent for the UI either way (record observed scale in the `Verified:` note).

---

## R4 — No-wipe cache-directory change (US7 / FR-025..027, Constitution IV)

**Question**: How to let the user change `ModelCacheDirectory` without silently moving or wiping the existing multi-GB cache?

**Evidence**: `FileSettingsService.UpdateAsync` persists `AppSettings` (which carries `ModelCacheDirectory`) and is consent-gated; it does **not** touch the model cache files. `AppSettings` is an immutable record updated via `with`.

**Decision**: The Settings UI reads current settings (`GetAsync`), and on save: (a) **validates** the new directory is writable/exists — invalid/unwritable ⇒ honest error, retain previous valid value (FR-027); (b) surfaces a **warn/confirm** (reusing `ConfirmDialog`) stating the consequence — *"existing cached models under the old directory may no longer appear; nothing will be moved or deleted"* — before persisting (FR-026); (c) on confirm, `UpdateAsync(settings with { ModelCacheDirectory = newDir })`. **No file move, no delete** is ever performed by M3.

**Rationale**: The setting is just a pointer; honoring data preservation means changing the pointer only, never the data, with the consequence surfaced (FR-026). **Alternative rejected**: auto-migrating the cache (a multi-GB destructive/move side-effect — explicitly forbidden by FR-026 and Constitution IV).

---

## R5 — KI-009 resolution (cached vs available grouping, US4 / FR-016..018)

**Question**: How to compute the cached group reliably without the `BrowseAsync(CachedOnly)` double-filter dropping genuinely-cached models?

**Evidence**: `BrowseAsync` (L33-39) sources from `GetCachedModelsAsync` when `CachedOnly` then re-applies `filter.Apply` which drops `!IsCached` (mapped from FL `info.Cached`). KI-009: if FL `Info.Cached` isn't reliably true on the cached-models path, the cached browse returns empty despite cached models. `ListCachedAsync` (L67-73) sources directly from `GetCachedModelsAsync` and does **not** re-apply `CachedOnly`.

**Decision**: For M3 grouping, **trust the authoritative cached source** — use `ListCachedAsync()` (already correct: sources from `GetCachedModelsAsync`, no `CachedOnly` re-filter) for the cached group, and `BrowseAsync()` (full list, no `CachedOnly`) for the universe. Grouping logic is a pure `CatalogGrouping.Partition(all, cachedAliases)` seam in Core that places each model in exactly one group by membership in the authoritative cached set — **not** by re-reading `info.Cached`. On hardware, **verify** whether FL `Info.Cached` is authoritative on the cached path; if it is, the per-card badge stays correct; if it isn't, the grouping still works because it keys off the cached-list membership, not the flag. Either outcome resolves KI-009; record the observed FL `Info.Cached` semantics in the `Verified:` note and close/annotate KI-009.

**Rationale**: This is exactly the KI-009 workaround mandated by FR-017 ("trust `GetCachedModelsAsync` or verify `Info.Cached`"). **Alternative rejected**: keep using `BrowseAsync(CachedOnly)` for the cached group (the double-filter that KI-009 warns about).

---

## R6 — Loaded-state reflection & the loaded-model limit (US2 / FR-007..008)

**Question**: How do cards reflect loaded state without staleness, and how is the FL loaded-model limit surfaced honestly?

**Evidence**: `ListLoadedAsync` (L75-80) returns the authoritative loaded set from `GetLoadedModelsAsync`. `MapEnriched` already sets `IsLoaded` from the loaded-id set. Load/unload route through `_gate.MutateAsync(..., Drain, ...)`.

**Decision**: After any mutation (load/unload/delete/auto-load) completes, **re-read the authoritative loaded + cached lists** and recompute the grouped view + per-card `IsLoaded` (refresh-on-mutation, not optimistic flips) → no stale state (FR-007). A single app-wide "currently loaded" indicator is derived from `ListLoadedAsync`. The **loaded-model limit** is whatever FL enforces at the pinned version: if FL throws/rejects a load at capacity, surface that honestly (state what to unload, FR-008) — do not assert a fixed number. **PIN for e2e/research**: observe FL's behavior at capacity on hardware and surface the real message.

**Rationale**: Authoritative re-read is the honesty-preserving source (FR-007). **Alternative rejected**: optimistic local toggling (risks stale/false state the spec forbids).

---

## R7 — Variant targeting (US5 / FR-019..021) — the one additive service delta

**Question**: A pinned variant must be honored by download/load (FR-020), but `IFoundryCatalogService.DownloadAsync(alias)` / `LoadAsync(alias)` take no variant.

**Evidence**: FL exposes `IModel.SelectVariant(IModel variant)` (sync, void) and `get_Variants()`. The Core `GetVariantsAsync` returns `ModelVariant` records carrying `VariantId` (= the FL variant `Info.Id`). The current service resolves a model by alias and downloads/loads its **default** variant; there is no path to honor a pin.

**Decision**: Add a **back-compatible, additive** optional parameter to the two targeting methods:
```csharp
Task DownloadAsync(string alias, IProgress<double>? progress = null, string? variantId = null, CancellationToken ct = default);
Task LoadAsync(string alias, string? variantId = null, CancellationToken ct = default);
```
In `FoundryCatalogService`, when `variantId` is non-null, resolve the matching `IModel` from `model.Variants` (by `Info.Id`) and call `model.SelectVariant(variant)` before download/load; when null, behave exactly as today (default variant). The pure `VariantSelectionState` seam (Core) tracks which variant is pinned per model (default = none/first, no-variants case = honest "no variants reported") and is unit-tested dylib-free (FR-021/FR-034). This is **not** a new service or a layer breach — it is an optional parameter on an existing seam (Constitution V intact; no Complexity Tracking entry).

**Rationale**: Honoring a pin (FR-020) genuinely requires passing the variant to FL; the additive optional parameter is the minimal change that keeps every existing caller compiling and the UI FL-free. **Alternatives rejected**: a separate `SelectVariantAsync` stateful call on the service (introduces hidden per-model server state and races); ignoring the pin (violates FR-020).

---

## R8 — Delete consent gate (US3 / FR-010..015, Constitution IV — load-bearing)

**Evidence**: `DeleteFromCacheAsync(alias, userConfirmed, ct)` (L120-136) throws `InvalidOperationException` when `!userConfirmed` **before** any FL call, then routes the actual delete through the gate (Drain) so it can't tear an in-flight op (FR-015).

**Decision**: The enforcement point already exists — M3 builds the **two-step UI**: Delete activates `ConfirmDialog` (names the exact model, states disk is freed); Cancel = no-op (FR-012); Confirm calls `DeleteFromCacheAsync(alias, userConfirmed: true, ct)` (FR-013). The consent gate is proven **dylib-free** by a unit test asserting `userConfirmed:false` throws and removes nothing (SC-006) — using a fake/stub service or asserting the guard clause is reached before any FL resolution. There is no one-click destructive path anywhere (FR-014).

**Rationale**: Reuses the already-correct, already-gated service method; the only new surface is the consent UI. **Alternative rejected**: a single-click delete with undo (still a destructive default — forbidden).

---

## R9 — Evidence path (KI-001) & data-preservation-safe verification (FR-035/036)

**Decision**: Autonomous verification uses **DevFlow DOM inspection** (`webview source` / `Runtime evaluate`) per KI-001 — the WKWebView layer only screenshots when frontmost, so DOM is the sanctioned evidence; a frontmost-window screenshot / human eyeball is the supplement, not the gate. The DevFlow e2e (quickstart.md) downloads a **small test model the test itself selects** (e.g. `qwen2.5-0.5b`), exercises download-progress/cancel, load/unload + loaded indicator, grouping, variant select, disk warn, and performs the **only live delete on that test-downloaded model** — never on pre-existing user cache (FR-036). The consent gate + confirm-flow are proven without any live delete (unit + DOM-of-the-dialog).

**Rationale**: Directly satisfies KI-001 + FR-035/036 + Constitution II/IV. **Alternative rejected**: relying on `ui screenshot` of WebView content (blank for background apps — KI-001).

---

## Open runtime unknowns to pin during M3 (recorded, not guessed)

| # | Unknown | Resolve during |
|---|---------|----------------|
| U1 | Does FL honor the download `CancellationToken` mid-transfer? (R1) | DevFlow e2e; record in `Verified:` / KNOWN-ISSUES if not honored |
| U2 | `Action<float>` progress scale (0..1 vs 0..100) (R3) | DevFlow e2e; normalize in mapping |
| U3 | Is FL `Info.Cached` authoritative on the cached-models path? (R5/KI-009) | DevFlow e2e; grouping keys off cached-list either way |
| U4 | FL loaded-model limit + at-capacity behavior (R6) | DevFlow e2e; surface real message |
