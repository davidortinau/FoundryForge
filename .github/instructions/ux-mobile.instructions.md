---
description: Medium-specific UX reasoning for mobile (phone) application UI.
applyTo: "**/*.{xaml,axaml,cs,razor,swift,kt,xml,tsx,jsx,vue}"
---

# Mobile UX (medium layer)

Apply alongside the repo-wide UX first-principles instructions when the target is a **phone app** (touch; small screen; one-handed; single-task focus). This file holds only what the mobile medium changes. Tune the `applyTo` glob to your mobile UI folders.

**Defining facts:** coarse fingertip input that occludes its target, small screen at arm's length, frequent one-handed/on-the-move use, one app in focus.

**What changes (often the mirror image of desktop):**
1. **Target size is a floor, not a budget** — large, well-spaced tap targets; no tiny dense controls.
2. **No hover** — never put essential info/actions behind hover; surface persistently or behind an explicit, signified tap/long-press. (Most common desktop-to-mobile porting bug.)
3. **Density must drop** — reduce per-screen content, shorten nav depth, accept more screens/scrolling; lead with the one primary task.
4. **Thumb zones** — primary/frequent actions in the easy bottom-center reach; rare/destructive actions harder to hit by accident.
5. **Single-surface, single-task** — focused linear flow; carry context forward (recall is costly with no room to keep things visible).
6. **Gestures replace right-click/precision** — signpost swipe/long-press/pinch and always give a visible button path.

**Render-time handoff (do in styling, not design):** exact minimum tap-target metric, system fonts, control styling, corner radii, platform nav chrome, and OS-global gestures (system back/home/edge). Everything structural (tab bar vs. drawer, bottom-sheet vs. full screen, flow depth) is a design decision — both tabs and drawers are fine on either OS, so reason from principles and commit to one answer; don't fork per platform.
