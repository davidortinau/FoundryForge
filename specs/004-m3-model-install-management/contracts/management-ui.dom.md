# Contract: Management UI DOM surface (DevFlow verification hooks)

M3 extends the M2 catalog UI with **management actions**, exposing stable `id` / `data-testid` attributes so M3 is verified via DevFlow DOM inspection (KI-001 path: `webview source` / `Runtime evaluate`) without relying on `ui screenshot`. Components live under `src/FoundryStudio.App/Components/Catalog/` and `…/Components/Pages/`. This contract is what the DevFlow e2e and any future UI tests assert against. (M2 hooks in `specs/003…/contracts/catalog-ui.dom.md` remain valid; M3 **adds** the hooks below and **flips** the M2 negative invariants that are now intentionally present.)

## Cached vs Available grouping (US4 / FR-016)

| Element | id / data-testid | Notes |
|---------|------------------|-------|
| Cached group section | `data-testid="group-cached"` | labeled "Installed / cached"; from authoritative cached set (KI-009) |
| Available group section | `data-testid="group-available"` | labeled "Available to download" |
| Group label (each) | `data-testid="group-label"` | text label, not color-only (WCAG AA) |
| Per-group count | `data-testid="group-count"` `aria-live="polite"` | honest counts |

## App-wide loaded indicator (US2 / FR-007)

| Element | id / data-testid | Notes |
|---------|------------------|-------|
| Currently-loaded indicator | `data-testid="loaded-indicator"` | derived from `ListLoadedAsync`; names loaded model(s) or "none loaded" |

## Per-card management actions (extend `ModelCard.razor`)

| Element | id / data-testid | Wired to | Story/FR |
|---------|------------------|----------|----------|
| Download button | `data-testid="card-download"` | `DownloadAsync(alias, progress, variantId, ct)` | US1 / FR-001 |
| Download progress | `data-testid="card-download-progress"` + `role="progressbar"` `aria-valuenow` | real `IProgress<double>` percent; **indeterminate** when no source | US1 / FR-002 |
| Cancel download | `data-testid="card-download-cancel"` | `CancellationTokenSource.Cancel()` | US1 / FR-003 |
| Auto-load checkbox | `data-testid="card-autoload"` + `<label>` | `ModelOperationState.AutoLoadAfterDownload` | US1 / FR-004 |
| Download error | `data-testid="card-download-error"` `role="alert"` | honest diagnosed cause; card returns not-cached | US1 / FR-005 |
| Load button | `data-testid="card-load"` | `LoadAsync(alias, variantId, ct)` (gate) | US2 / FR-006 |
| Unload button | `data-testid="card-unload"` | `UnloadAsync(alias, ct)` (gate) | US2 / FR-006 |
| Loaded badge (card) | `data-testid="card-loaded-badge"` | from authoritative loaded set; text+icon, not color-only | US2 / FR-007 |
| Busy/limit message | `data-testid="card-busy"` `role="alert"` | honest `ModelBusyException` / at-capacity message | US2 / FR-008/009 |
| Delete button | `data-testid="card-delete"` | **opens** `ConfirmDialog` — does NOT delete | US3 / FR-010 |
| Variant selector | `data-testid="card-variant-select"` + `<label>` | `VariantSelectionState.Pin(variantId)`; honest "no variants reported" | US5 / FR-019/021 |
| Disk-fit note | `data-testid="card-diskfit"` | `DiskFit.Fits` (no warn) / `Warn` (non-blocking) / `Unknown` ("size unknown — can't check fit") | US6 / FR-022..024 |

## Confirm dialog (`ConfirmDialog.razor` — US3 / US7)

| Element | id / data-testid | Notes |
|---------|------------------|-------|
| Dialog root | `data-testid="confirm-dialog"` `role="dialog"` `aria-modal="true"` | hidden until activated |
| Dialog message | `data-testid="confirm-message"` | **names the exact model** + states disk is freed (FR-011) / cache-dir consequence (FR-026) |
| Confirm button | `data-testid="confirm-accept"` | calls `DeleteFromCacheAsync(alias, userConfirmed:true, ct)` (FR-013) or cache-dir `UpdateAsync` |
| Cancel button | `data-testid="confirm-cancel"` | no-op; closes dialog; nothing changes (FR-012) |

## Settings / cache directory (`Settings.razor` — US7)

| Element | id / data-testid | Wired to | FR |
|---------|------------------|----------|----|
| Settings page root | `data-testid="settings-page"` | route e.g. `/settings` | — |
| Cache-dir display/input | `id="settings-cache-dir"` + `<label>` | `AppSettings.ModelCacheDirectory` | FR-025 |
| Save button | `id="settings-cache-dir-save"` | opens warn/confirm, then `UpdateAsync` | FR-026 |
| Cache-dir error | `data-testid="settings-cache-dir-error"` `role="alert"` | invalid/unwritable; previous value retained | FR-027 |

## BYOM (OPTIONAL P3 — only if implemented, US8)

| Element | data-testid | Notes |
|---------|-------------|-------|
| BYOM import flow | `data-testid="byom-import"` | ONNX/Olive + `inference_model.json` only |
| BYOM reject (GGUF/safetensors) | `data-testid="byom-reject"` `role="alert"` | honest "ONNX-only — GGUF/safetensors not supported" (FR-029) |
| BYOM docs link | `data-testid="byom-docs"` | Olive/ONNX docs (FR-028) |

## Consent & negative invariants (verified — Constitution IV / FR-010/014/032)

The DOM MUST satisfy, on real Apple Silicon:
- A single activation of `card-delete` produces a `confirm-dialog` and performs **zero** deletions; `confirm-cancel` deletes nothing (SC-005).
- There is **no** one-click destructive delete path anywhere (no element deletes without going through `confirm-accept`) (FR-014).
- **Zero** inference-parameter controls on any card or page (temperature/top_p/max-tokens/etc. are M4) (FR-032):
  ```
  [data-testid="param-temperature"], [data-testid="param-top-p"], [data-testid="param-max-tokens"]  // MUST be absent
  ```
- **Zero** GGUF import affordances; **zero** fabricated progress bars (a `card-download-progress` exists only while a real download runs) (FR-032).
- Every genuinely-unavailable value renders honest "unknown" (size/disk/variants) — never a fabricated verdict (FR-024/021).

## Accessibility contract (WCAG AA — FR-033)
- Every new control (download/cancel/auto-load, load/unload, delete/confirm/cancel, variant selector, cache-dir editor) has an associated `<label>` / `aria-label`, is keyboard-reachable, and announces state changes.
- Progress uses `role="progressbar"` with `aria-valuenow`/`aria-valuemin`/`aria-valuemax` (or `aria-busy` when indeterminate).
- The confirm dialog is `role="dialog" aria-modal="true"`, focus-trapped, Esc = cancel.
- Loaded / cached / busy state conveyed by **text + icon**, never color alone.
- Contrast meets AA (verified via computed-style query in DevFlow).
