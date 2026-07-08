# Contract: Catalog UI DOM surface (DevFlow verification hooks)

The catalog Blazor UI exposes **stable `id` / `data-testid` attributes** so M2 can be verified via DevFlow DOM inspection (KI-001 path: `webview source` / `Runtime evaluate`) without relying on `ui screenshot`. This contract is what the DevFlow checks and any future UI tests assert against. Components live under `src/FoundryForge.App/Components/Catalog/`.

## Page & regions

| Element | id / data-testid | Notes |
|---------|------------------|-------|
| Catalog page root | `data-testid="catalog-page"` | route `/` (replaces M1 Home smoke) or `/catalog` |
| Loading state | `data-testid="catalog-loading"` | shown while reaching ready / fetching (FR-014) |
| Error state | `data-testid="catalog-error"` | honest diagnosed cause + retry (FR-016) |
| Error retry button | `id="catalog-retry"` | re-invokes BrowseAsync (FR-016) |
| Empty (no match) state | `data-testid="catalog-empty"` | distinct from loading/error (FR-015) |
| Reset filters action | `id="catalog-reset"` | restores curated default (FR-007) |
| Result count (live region) | `data-testid="catalog-count"` `aria-live="polite"` | announces narrowing (US2 AC-4) |

## Controls

| Control | id / data-testid | Wired to |
|---------|------------------|----------|
| Search input | `id="catalog-search"` + `<label>` | `CatalogFilter.SearchText` (FR-005) |
| Device filter | `id="filter-device"` | `CatalogFilter.Device` (FR-006) |
| Task filter | `id="filter-task"` | `CatalogFilter.Task` |
| Provider filter | `id="filter-provider"` | `CatalogFilter.Provider` |
| Cached-only toggle | `id="filter-cached"` | `CatalogFilter.CachedOnly` |
| Curated/Full toggle | `id="view-toggle"` | `ViewMode` (FR-008/FR-009) |
| Curated label | `data-testid="curated-banner"` | "Recommended / curated" label (FR-008) |
| Refresh | `id="catalog-refresh"` | re-read (FR-017), no mutation |

## Per-model card

| Element | data-testid | Field |
|---------|-------------|-------|
| Card | `data-testid="model-card"` + `data-alias="{alias}"` | one per visible model (FR-003) |
| Alias | `data-testid="card-alias"` | always present |
| Display name | `data-testid="card-displayname"` | |
| Size | `data-testid="card-size"` | real GB or "unknown" (FR-010/FR-012) |
| Device / EP | `data-testid="card-device"` | device + execution provider or "unknown" |
| Context length | `data-testid="card-context"` | or "unknown" |
| Capabilities | `data-testid="card-capabilities"` | vision/tool/reasoning chips (only where FL declares; tool may show "unknown") |
| License | `data-testid="card-license"` | or "unknown" |
| Variants | `data-testid="card-variants"` | count/list or "no variants reported" |
| Cached badge | `data-testid="card-cached-badge"` + text "Cached"/"Not cached" | semantic, NOT color-only (FR-004) |

## Negative invariants (verified absent — FR-013/SC-008)

The DOM MUST contain **zero** elements matching download / load / unload / delete / variant-select / chat affordances on any card:
```
[data-testid="card-download"], [data-testid="card-load"], [data-testid="card-delete"],
[data-testid="card-variant-select"], [data-testid="card-chat"]   // all MUST be absent
```

## Accessibility contract (WCAG AA — FR-018, SC-010)
- Every control has an associated `<label>` / `aria-label`.
- Cached state conveyed by **text + icon**, never color alone (FR-004).
- Cards and controls are keyboard-reachable; focus order is logical; state badges are announced.
- Result-count region is `aria-live="polite"`.
- Loading/empty/error states use roles/labels so assistive tech announces them (US6 AC-4).
- Contrast meets AA (verified via computed-style query in DevFlow).
