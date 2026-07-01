---
description: Medium-specific UX reasoning for tablet application UI (the hybrid medium).
applyTo: "**/*.{xaml,axaml,cs,razor,swift,kt,xml,tsx,jsx,vue,css,scss}"
---

# Tablet UX (medium layer)

Apply alongside the repo-wide UX first-principles instructions when the target is a **tablet** (touch-first, large screen, often two-handed, sometimes pointer/keyboard, capable of split-view). The trickiest medium: it inherits from both neighbors and contradicts each in places. Tune the `applyTo` glob to your tablet UI folders.

**Defining trait — intermittency:** the same app may be pure-touch then pointer+keyboard, fullscreen then half-width in split-view.

**Inherits from mobile:** assume fingertips → touch-sized targets as the floor (don't drop to desktop density); no guaranteed hover → hover is at most a progressive enhancement, never required; signpost gestures with visible alternatives.

**Inherits from desktop:** large screen → raise *information* density via multi-pane (master-detail) layouts, persistent sidebars, more in view than a phone — while keeping *touch* sizing. Supports split-view/slide-over, so the app may be half-width unexpectedly.

**Unique to tablet:**
1. **Two-handed reach** — comfortable zones shift to the left/right edges and bottom; center-top is hardest. Place primary controls along reachable edges, not stranded mid-screen.
2. **Adaptive layout is the core decision** — one pane or two (or more), and what happens as width changes between fullscreen and split-view. A tablet design that only works fullscreen is unfinished. Master-detail panes are progressive disclosure made spatial.

**Render-time handoff (do in styling, not design):** exact touch-target metric, system fonts, control styling, corner radii, multitasking chrome, and OS-global gestures (back/home/split-view). Everything structural (single-pane vs. master-detail, nav placement, how panes adapt) is a design decision — reason from principles and commit to one cross-platform answer; don't fork per OS.
