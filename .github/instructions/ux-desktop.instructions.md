---
description: Medium-specific UX reasoning for desktop application UI.
applyTo: "**/*.{xaml,axaml,cs,razor,swift,kt,xml,tsx,jsx,vue}"
---

# Desktop UX (medium layer)

Apply alongside the repo-wide UX first-principles instructions when the target is a **desktop app** (Windows/macOS/Linux; pointer + keyboard; large screen; multi-window). This file holds only what the desktop medium changes. Tune the `applyTo` glob above to the folders that actually hold your desktop UI.

**Defining facts:** precise pointer that doesn't occlude its target, hardware keyboard, large display viewed closely, app is one of several resizable windows.

**What changes:**
1. **Targets can be small and dense** — the cursor is a point; exploit screen edges/corners as easy targets. Spend density on power, not clutter.
2. **Hover exists and is usable** — tooltips, hover-reveal, previews are legitimate. (This is the thing that does NOT port to touch.)
3. **Density can be high** — multi-column grids, persistent sidebars/inspectors, smaller type; keep line length ~50–75 chars; keep hierarchy clear.
4. **Multi-window & resize resilience** — design for narrow-to-very-wide; secondary windows/palettes/inspectors are available.
5. **Pointer precision** — right-click menus, drag-select, modifier multi-select are first-class; never the *only* path to an action.
6. **Keyboard is primary** — shortcuts, logical tab order, visible focus indicator; this is also WCAG Operable.

**Render-time handoff (do in styling, not design):** exact control metrics, system fonts, corner radii, window chrome, standard OS shortcuts and window management. Everything structural (sidebar vs. top-nav, modal vs. inspector, nav depth) is a design decision — reason it from principles and commit to one cross-platform answer; don't fork per OS.
