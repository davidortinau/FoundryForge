# Quickstart: M3 — Model Install & Management (validation & DevFlow e2e)

This is the **validation/run guide** that proves M3 works end-to-end. It is not implementation. Implementation detail belongs in `tasks.md`. Two layers: (A) **dylib-free unit tests** for every Core seam + the consent gate (no hardware), and (B) a **real Apple-Silicon DevFlow DOM e2e** (Constitution II) whose only live delete is on a model the test itself downloaded — pre-existing user cache is never touched (FR-036).

## Prerequisites
- macOS / Apple Silicon; .NET 11 SDK (`global.json` pin); FL native chain present (M0 gate green).
- DevFlow Agent/Blazor `0.1.0-preview.8.26256.5` (KI-002).
- Evidence path = **DOM inspection** (KI-001): `maui devflow webview source` / `Runtime evaluate`. `ui screenshot` only as a frontmost-window supplement, never the gate.
- A **small test model** the test selects (e.g. `qwen2.5-0.5b`) — used for the live download + the single live delete.

## Layer A — dylib-free unit tests (no hardware, CI seam gate)
```bash
dotnet test tests/FoundryStudio.Tests/FoundryStudio.Tests.csproj
```
Expected green, covering:
- `DiskFitHeuristicTests` — Fits / Warn / Unknown incl. null size + boundary + negative-free throws (SC-009).
- `CatalogGroupingTests` — exactly-one-group partition; KI-009 cached-set trust (SC-007).
- `VariantSelectionStateTests` — default / pin / pin-unknown-ignored / no-variants (SC-008).
- `DeleteConsentGateTests` — `DeleteFromCacheAsync(userConfirmed:false)` throws and removes nothing, before any FL call (SC-006). **This proves the consent gate without any live cache.**
- Existing suites stay green (`RamFitHeuristicTests`, `ModelStateGateTests`, `SettingsDocumentTests`, …).

## Layer B — DevFlow DOM e2e (real Apple Silicon)
Launch to ready, navigate `/`, then verify each story via DOM. Reference: `contracts/management-ui.dom.md` (hooks), `contracts/service-surface.md` (wiring), `data-model.md` (state).

1. **Grouping (US4 / SC-007, KI-009)** — assert `data-testid="group-cached"` and `group-available` exist; every `model-card` is in exactly one; the cached group matches `ListCachedAsync` and is **not empty** when cached models exist. Record observed FL `Info.Cached` semantics (research U3).
2. **Disk-fit (US6 / SC-009)** — a card whose `SizeGb` > free disk shows `data-testid="card-diskfit"` = Warn (non-blocking; download still enabled); an unknown-size model shows honest "size unknown — can't check fit". Free disk read via `DriveInfo` on the cache dir.
3. **Variant select (US5 / SC-008)** — a multi-variant model exposes `card-variant-select`; pinning sets the value passed to download/load; a no-variant model shows honest "no variants reported" with zero fabricated options.
4. **Download + progress + cancel (US1 / SC-001/002)** — on the test model, activate `card-download`; assert `card-download-progress` (`role="progressbar"`, `aria-valuenow`) advances from the **real** `IProgress<double>` callback (not fabricated). Then activate `card-download-cancel`; assert the card returns to not-cached/idle and the model is **absent** from `ListCachedAsync` (no partial shown as ready). Record FL cancel-honoring behavior (research U1); if not honored, annotate KNOWN-ISSUES.
5. **Download → auto-load (US1 / SC-003)** — re-download the test model with `card-autoload` checked; on completion assert `card-loaded-badge` + `loaded-indicator` reflect loaded.
6. **Load / unload + indicator (US2 / SC-004)** — unload then load via `card-unload`/`card-load`; assert `loaded-indicator` and all cards reflect the change with **no stale state** (refresh-on-mutation). Force a busy/at-capacity path and assert honest `card-busy` (`ModelBusyException` / what-to-unload), no hang (research U4).
7. **Delete CONFIRM FLOW — data-preservation (US3 / SC-005, Constitution IV)** —
   - Activate `card-delete` → assert a `confirm-dialog` opens that **names the test model** + states disk is freed; assert **nothing deleted** on this single activation.
   - Activate `confirm-cancel` → dialog closes, model **still cached**, nothing deleted.
   - Re-open, activate `confirm-accept` → model removed via `DeleteFromCacheAsync(userConfirmed:true)`; card moves to Available. **This live delete targets only the test-downloaded model — never pre-existing user cache (FR-036).**
8. **Cache directory (US7 / SC-010)** — open `/settings`; assert `settings-cache-dir` shows the current value; change to an invalid dir → `settings-cache-dir-error`, previous value retained; change to a valid dir → warn/confirm dialog states "nothing moved or deleted" → persists; assert **0** existing cache files moved/wiped.
9. **Negative invariants (SC-011)** — assert **zero** inference-param controls, **zero** GGUF import paths, **zero** fabricated progress bars, and every unavailable value renders honest "unknown".

## Definition of Done (M3)
- [ ] Layer A unit tests green (CI seam gate clean).
- [ ] Layer B DevFlow DOM e2e passes on real Apple Silicon (steps 1–9).
- [ ] Pre-existing user cache untouched; the only live delete was the test-downloaded model (FR-036).
- [ ] BYOM (US8) optional — its absence does **not** block DoD (FR-030).
- [ ] Independent reviewer (not the author) signs off (Constitution II/III).
- [ ] Milestone note ends with a **`Verified:`** line naming the checks that ran (unit suites + the DevFlow DOM e2e + observed FL cancel/`Info.Cached`/loaded-limit behavior), and KI-009 closed/annotated.

> `Verified:` (to be filled at close) — e.g. `Verified: dotnet test green (DiskFit/Grouping/VariantSelection/DeleteConsentGate + existing); DevFlow DOM e2e on M-series — download(progress real)/cancel/auto-load/load/unload/loaded-indicator/group/variant/disk-warn/delete-confirm-on-test-model; user cache untouched; FL cancel honored=<obs>, Info.Cached authoritative=<obs>, loaded-limit=<obs>; KI-009 resolved.`
