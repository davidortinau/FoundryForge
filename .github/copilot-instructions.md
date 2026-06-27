# Copilot instructions — UX first principles

When generating, structuring, or reviewing application UI, reason like a senior UI/UX designer **before** styling: decide *what* and *why* from first principles, and leave *how it looks* (color, type, spacing tokens, brand, component libraries) to the project's styling conventions. This file governs design *reasoning*, not execution.

## The governing rule
Design principles are universal; their application changes by **medium** (desktop / tablet / mobile), not by **platform** (iOS / Android / Windows). Reason from principles, decide once, and let a thin render-time layer handle mechanical platform substitutions (exact control sizing, system fonts, corner radii, OS-global gestures). Do **not** fork a structural decision — tabs vs. drawer, modal vs. inspector, single-pane vs. master-detail, navigation depth — by platform; the cross-platform overlap of acceptable patterns is wide, and forking fragments one coherent design into three.

## How to make a UI decision
1. Name the decision (layout, flow, affordance, grouping).
2. Pull the 2–4 relevant principles — don't recite all of them.
3. Surface the tension between them.
4. Commit to one answer and justify it in a sentence a designer would recognize.
5. Check the medium (see the medium-specific instruction files).
6. Treat accessibility as a constraint on the decision, not a later patch.

## The principles (reason from these)

**Laws & effects (why users behave as they do):** Fitts (big/close targets are fast; size depends on input device), Hick (fewer choices = faster decisions), Miller (chunk; ~4 items in working memory), Jakob (honor broad conventions), Tesler (absorb complexity into the system), Aesthetic-Usability (polish buys forgiveness but can mask problems), Doherty (keep response/feedback under ~400ms perceived), Goal-Gradient (show progress), Peak-End (invest in the peak and the ending), Serial Position (privilege first/last), Von Restorff (make the one key action distinct; emphasis is zero-sum), Zeigarnik (visible incompleteness draws completion).

**Usability heuristics (what to aim for):** visibility of system status; match to the real world; user control & freedom (undo/exit); consistency & standards; error prevention; recognition over recall; flexibility & shortcuts; minimalist design; clear error recovery; help. Plus Norman: affordances, signifiers, mapping, feedback, constraints — does the UI communicate how it works?

**Compositional & perceptual (how the eye organizes a surface):** clear visual hierarchy (one dominant element, not flat); contrast; Gestalt grouping (proximity, similarity, common region — group related, separate distinct); alignment to a shared grid; progressive disclosure (defer rare/advanced by frequency of use); consistency.

**Accessibility & ethics (a constraint on every decision):** WCAG POUR — Perceivable (contrast; never meaning by color alone), Operable (keyboard path + visible focus on desktop; adequate target size/spacing on touch; motion safety), Understandable (plain language, predictable, labeled fields, helpful errors), Robust (standard, named controls). Honor user settings (reduced motion/contrast, larger text). Reject dark patterns (confirmshaming, fake urgency, hidden costs, asymmetric cancel) — the same effects that make UI good become manipulative when aimed against the user.

## Boundary
This is reasoning, not execution. Do not pick colors, fonts, spacing scales, or component libraries here, and do not enforce a brand — those are owned by the project's styling setup. Keeping the boundary clean lets this guidance compose with styling instructions instead of conflicting.

## Attribution
The principle concepts (Fitts, Hick, Nielsen's heuristics, Gestalt, etc.) are from the public HCI/design literature, described here in original wording. Jon Yablonski's *Laws of UX* (lawsofux.com) is the best-known collection and is CC BY-NC-ND — cite it as further reading, don't reproduce it. Credit Jakob Nielsen / NN/g for the usability heuristics.


<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
specs/006-m5-server-toggle/plan.md
<!-- SPECKIT END -->
