# Quickstart: M2 — Catalog Browse + Discovery (validation guide)

This is the **end-to-end validation guide** that proves M2 works. It is a run/verify guide — implementation lives in `tasks.md` and the source tree. References: `contracts/` (DOM hooks + service surface), `data-model.md` (enriched DTOs), `research.md` R6 (screenshot decision).

**Platform**: real Apple Silicon Mac, `net11.0-macos` AppKit head (DEC-004/DEC-016). Verification follows KI-001 (DOM primary; re-test `ui screenshot` per R6).

---

## Prerequisites
- M1 foundation green (DEC-016): app launches → `AppReadyBoundary` "initializing" → "ready".
- Foundry Local `1.2.3` dylib chain bundled/signed (DEC-005); FL catalog reachable (~46 models, DEC-011).
- DevFlow tooling available (`maui devflow ...`), DEBUG build.

## Build & unit tests (dylib-free gate)
```bash
dotnet build FoundryStudio.sln                       # 0 errors
dotnet test tests/FoundryStudio.Tests                 # all green, no native dylib needed
```
Expected: existing M1 tests still pass **plus** new M2 tests for `CapabilityParser`, `CuratedSelector`, `CatalogFacets`, the FL→Core mapping pure transform, and updated `CatalogFilter`/null-device cases (FR-019, SC-003/SC-004).

## Launch
```bash
# Run the inner binary directly (KI-004: app exits under `open`/`dotnet watch`)
src/FoundryStudio.App/bin/Debug/net11.0-macos/.../FoundryStudio.app/Contents/MacOS/FoundryStudio
```

---

## Scenario validation (DevFlow DOM)

Drive each via `maui devflow webview source` and `maui devflow webview Runtime evaluate "<js>"`.

### V1 — Catalog renders with cards + cached badges (US1, SC-001/SC-002)
1. Wait for ready, confirm `[data-testid="catalog-page"]` present, `catalog-loading` gone.
2. `document.querySelectorAll('[data-testid=model-card]').length` ≥ 1 (one per FL model).
3. Each card has `card-alias` and a `card-cached-badge` with text "Cached"/"Not cached".
4. Cross-check: cached badges match `ListCachedAsync` set (badge value = real cached state).
5. Confirm **no** mutation affordances exist (negative invariants in `catalog-ui.dom.md`).

### V2 — Search narrows the list (US2, SC-003)
1. Set `#catalog-search` value to a known alias substring; dispatch `input`.
2. Visible `model-card` count narrows to alias/displayname/id matches (case-insensitive).
3. Search a nonsense string → `[data-testid="catalog-empty"]` shown; clear → full list restored.
4. Confirm `catalog-count` live region updated. Unit test reproduces the same narrowing over `CatalogFilter`.

### V3 — Filters + cached-only intersect (US3, SC-004)
1. Select device / task / provider via `#filter-device`/`#filter-task`/`#filter-provider`; toggle `#filter-cached`.
2. Visible set reduces to the AND-intersection; matches `CatalogFilterExtensions.Matches`.
3. Select a facet with no members → honest empty state (not error). `#catalog-reset` restores default.
4. Confirm an unknown-facet model is not mislabeled into a real facet (US3 AC-6).

### V4 — Curated default + full toggle (US4, SC-005)
1. Fresh launch, no search/filter → `[data-testid="curated-banner"]` present; visible set = `CuratedSelector.Select`.
2. `#view-toggle` reveals full catalog.
3. Search for a **non-curated** model while in curated mode → it is found (search runs over full catalog, FR-009).

### V5 — Rich honest metadata (US5, SC-006/SC-007)
1. For a known model, inspect card fields: `card-size` (real GB, not `0`), `card-device`, `card-context`, `card-capabilities`, `card-license`, `card-variants` reflect real FL metadata (research.md R1).
2. Find at least one field FL omits for some model → renders "unknown / not provided" (never `0 GB`/fabricated).
3. Confirm 0 cards show M1 stub defaults for models where FL provides real values.

### V6 — Honest states (US6, SC-009)
1. Loading state visible during init/fetch.
2. Over-constrained filter → distinct `catalog-empty` with reset.
3. Simulate/force a catalog fetch failure → `[data-testid="catalog-error"]` names the diagnosed cause + `#catalog-retry`; never a silent empty list.

### V7 — Accessibility (FR-018, SC-010)
- All controls labeled; tab order logical; cached badge has text+icon (not color-only).
- `aria-live` count announces; contrast AA via computed-style query.

### V8 — Non-mutation (FR-013, SC-008)
- Record cached state of a non-cached model; run a full browse/search/filter/refresh session; re-check → unchanged (0 FL download/load triggered).

---

## Screenshot re-verification (R6 / KI-001)
```bash
maui devflow ui screenshot --output /tmp/m2-catalog.png
```
- **If it captures the catalog WebView** → attach as evidence; update KI-001 → "upstream-fixed-pending-removal".
- **If still blank** → fall back to DOM snapshots + native window capture; keep KI-001 open; record honestly.

## Closing the milestone (Constitution II, FR-023/FR-024)
- Run V1–V8 on real Apple Silicon; capture DOM evidence (+ screenshots per R6).
- Confirm KI-008 dispose hardening done, or record its deferral honestly (FR-022/SC-012).
- Independent reviewer (not the author) approves (FR-024/SC-011).
- End the closing note with a `Verified:` line naming every check that ran (e.g. "Verified: net11.0-macos build 0 errors; N/N xUnit dylib-free; DevFlow DOM V1–V8 on Apple Silicon; screenshot path re-tested [captured|still-blank per KI-001]; 0 FL download/load during browse").
