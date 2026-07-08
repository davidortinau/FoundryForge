# FoundryForge Design Guide

> The durable design system for FoundryForge. Every surface — current and future (chat, server,
> settings) — is built from the tokens, components, and rules below. This guide is **enforceable**:
> if a screen contradicts it, the screen is wrong. It is paired with the Constitution — where the
> two touch (honesty, data preservation), the Constitution wins and this guide encodes how to
> express it visually.

Status: v1.0 · Scope: macOS (Apple Silicon) · Stack: .NET MAUI + Blazor Hybrid (`net11.0-macos`)

---

## 1. Vision & principles

**Vision:** FoundryForge is the calm, official-feeling forge for local AI — models are inspected,
**cast** (downloaded), **tempered** (loaded), and **served** with visible truth at every step.

**North-star words:** **Trustworthy · Elemental · Precise.**

Principles (in priority order):

1. **macOS-native first.** System typography (SF), vibrancy, rounded surfaces, restraint. We look
   like we belong next to Xcode and Sherpa, not like a cross-platform web app in a window.
2. **Honesty is visual.** Motion and state only ever reflect *real* events — bytes moving, a model
   actually loading, tokens actually streaming. No fabricated progress, no fake "ready," no
   capability we can't prove. (Constitution §III.) This is also our competitive wedge.
3. **One confident accent.** Exactly one brand color — **Foundry Copper**. Everything else is
   neutral surface or *semantic* color (positive/warning/danger/info). Copper never means "healthy"
   or "safe."
4. **Consent is a first-class pattern.** Destructive actions (delete cached model, reset cache) use
   the consent pattern in §11. No one-click destruction, ever. (Constitution §IV.)
5. **Density with air.** Dense, scannable data (model metadata, logs, metrics) inside calm,
   well-spaced surfaces. Borrow LM Studio's information density, Sherpa's composure.
6. **Accessible by default.** WCAG 2.1 AA minimum: contrast, focus-visible, full keyboard
   operation, `prefers-reduced-motion`, correct ARIA. Non-negotiable.

**The motif, stated once:** FoundryForge is a **workshop, not a robot companion.** The forge
metaphor is *state language and subtle material atmosphere* — never decoration, never kitsch.
LM Studio owns the friendly-mascot space; we own governed, transparent, native craft.

---

## 2. Identity

- **Accent:** Foundry Copper (see §3). It is the single unmistakable FoundryForge mark. We use a
  Fluent-2 *structure* (spacing, elevation, neutral discipline, WCAG-first color) but **do not**
  adopt Fluent blue as identity — copper says "local forge, visible compute, heat under control."
- **No mascot.** Instead, a **foundry mark**: a small stamped copper glyph (a blackened steel tray
  with a single molten-copper pour forming a subtle "F" channel — no flames, hammers, or cartoon
  anvils). It appears in onboarding, empty states, and confirmations as a maker's mark.
- **App/dock icon:** rounded macOS squircle, blackened steel tray + one copper pour "F". Reads as
  controlled heat + local craft + precision tooling.
- **Tone:** governed, transparent, quietly characterful. Warm but never hypey.

---

## 3. Color system

Two themes at full parity: **Workshop Daylight** (light) and **Night Forge** (dark). All UI color
flows through the semantic tokens below — components never hardcode hex. Token names map 1:1 to
`wwwroot/tokens.css` (§13).

### 3.1 Brand / accent — "Foundry Copper"

| Token | Light | Dark | Use |
|---|---|---|---|
| `--fs-accent` | `#B94A18` | `#FF9E4A` | Primary actions, active selection, real-progress highlight, brand moments |
| `--fs-accent-fill` | `#B94A18` | `#E36F22` | Filled control background (hot fill in dark) |
| `--fs-accent-text` | `#8F3510` | `#FF9E4A` | Copper used as *text/link* (AA-safe on canvas/surface) |
| `--fs-accent-ring` | `#B94A18` @ 45% | `#FF9E4A` @ 55% | Focus ring when action is app-primary |

**Copper is used for:** primary buttons (Download, Load, Start server), sidebar active
indicator, *real* download/load/generation progress fills, first-run + loaded-confirmation brand
moments, primary focus rings.

**Copper is NEVER used for:** success, warning, danger, info; capability claims; errors; "model will
fit" confidence; anything a user could mistake for health or safety. When meaning matters, a
**semantic** color takes over.

### 3.2 Workshop Daylight (light)

| Role | Token | Hex |
|---|---|---|
| Canvas | `--fs-canvas` | `#F7F4EF` |
| Surface / card | `--fs-surface` | `#FFFFFF` |
| Surface raised | `--fs-surface-raised` | `#FBFAF7` |
| Sidebar grouped surface | `--fs-surface-sidebar` | `#EEE9E1` |
| Inset surface | `--fs-surface-inset` | `#E6DFD4` |
| Border subtle | `--fs-border` | `#D8D0C4` |
| Border strong | `--fs-border-strong` | `#B8AA9C` |
| Text primary | `--fs-text` | `#1E1A17` |
| Text secondary | `--fs-text-secondary` | `#5B5148` |
| Text tertiary | `--fs-text-tertiary` | `#7A6F65` |
| Text inverse | `--fs-text-inverse` | `#FFFFFF` |
| Positive | `--fs-positive` | `#167C3A` |
| Warning | `--fs-warning` | `#9A6700` |
| Danger | `--fs-danger` | `#C42B1C` |
| Info | `--fs-info` | `#005A9E` |

### 3.3 Night Forge (dark)

| Role | Token | Hex |
|---|---|---|
| Canvas / coal bed | `--fs-canvas` | `#0E0D0B` |
| Surface / black iron | `--fs-surface` | `#15120F` |
| Surface raised / warm steel | `--fs-surface-raised` | `#1D1813` |
| Surface elevated / heated plate | `--fs-surface-elevated` | `#261E17` |
| Sidebar grouped surface | `--fs-surface-sidebar` | `#12100E` |
| Inset surface | `--fs-surface-inset` | `#0A0908` |
| Border subtle | `--fs-border` | `#332820` |
| Border strong | `--fs-border-strong` | `#5C4637` |
| Text primary | `--fs-text` | `#F5EFE7` |
| Text secondary | `--fs-text-secondary` | `#C8BAAA` |
| Text tertiary | `--fs-text-tertiary` | `#8E8174` |
| Text inverse | `--fs-text-inverse` | `#170E08` |
| Positive | `--fs-positive` | `#6CCB5F` |
| Warning | `--fs-warning` | `#F5C542` |
| Danger | `--fs-danger` | `#FF6B5F` |
| Info | `--fs-info` | `#5CB8FF` |

### 3.4 Semantic-tint surfaces

For status banners/pills, derive a soft tinted background from the semantic color at ~12–16% over
the surface (light) / ~18–22% (dark). Tokens: `--fs-positive-bg`, `--fs-warning-bg`,
`--fs-danger-bg`, `--fs-info-bg`. Text uses the full-strength semantic color and must hit AA.

### 3.4a Capability accents (Discover)

At-a-glance model capability marks (Vision / Tool use / Reasoning) use three dedicated hues so they
read as *capabilities*, not status or brand. They are used ONLY for the small capability icons on
Discover cards and the filled capability pills in the detail sidebar — **never** on operable
controls, and never to imply a capability we can't prove (a capability is shown only when the model
actually declares it; unknown ⇒ omitted, never asserted absent). Tokens (light / dark):

| Role | Token | Light | Dark |
|---|---|---|---|
| Vision | `--fs-cap-vision` | `#B26F16` | `#E0A64B` |
| Tool use | `--fs-cap-tools` | `#3F7C89` | `#6FB6C4` |
| Reasoning | `--fs-cap-reasoning` | `#4C7343` | `#86C177` |

Pill/tint backgrounds: `--fs-cap-vision-bg`, `--fs-cap-tools-bg`, `--fs-cap-reasoning-bg`
(derived at ~13% light / ~18% dark over the surface).

### 3.5 Contrast rules

- Body text on its surface targets AA (≥4.5:1); large/page titles ≥3:1, prefer AA.
- Copper as **text** uses `--fs-accent-text` (`#8F3510` light, `#FF9E4A` dark) — never bright
  `#B94A18`/`#E36F22` as small text on light.
- On a copper **fill**, label is `--fs-text-inverse`. Verify the specific pairing is AA.

---

## 4. Typography

Native SF stack; no novelty display face.

- Family: `-apple-system, BlinkMacSystemFont, "SF Pro Text", "SF Pro Display", "Segoe UI", sans-serif`.
- Mono (endpoints, model IDs, ports, tokens/sec, logs, UDIDs): `"SF Mono", ui-monospace, "Cascadia Code", monospace`.

| Token | Size / weight / line | Use |
|---|---|---|
| `--fs-type-display` | 30px / 700 / 1.15 | Page title (SF Pro Display feel) |
| `--fs-type-title` | 20px / 600 / 1.25 | Section / card title |
| `--fs-type-subtitle` | 15px / 400 / 1.4 (text-secondary) | Page/section subtitle |
| `--fs-type-body` | 14px / 400 / 1.5 | Body, controls |
| `--fs-type-label` | 13px / 600 / 1.3 | Field labels, chips |
| `--fs-type-eyebrow` | 12px / 600 / 1.2, +0.06em, uppercase, tertiary | Section eyebrow (e.g. "INSTALLED / CACHED") |
| `--fs-type-mono` | 13px / 450 / 1.45 | Code/endpoints/metrics |

---

## 5. Space, radius, elevation, motion

**Space (4px base):** `--fs-space-1:4` `-2:8` `-3:12` `-4:16` `-5:24` `-6:32` `-7:48`.

**Radius:** `--fs-radius-sm:8` (chips/inputs) · `-md:12` (buttons) · `-lg:16` (cards) · `-xl:20`
(dialogs/large panels) · `--fs-radius-pill:999`.

**Elevation:**
- `--fs-elev-card`: `0 1px 2px rgba(0,0,0,.06), 0 1px 1px rgba(0,0,0,.04)` (light) / soft warm in dark.
- `--fs-elev-overlay`: `0 18px 50px rgba(0,0,0,.18)` for dialogs/palettes.
- Dark mode: shadows are weaker; separation comes from the surface ramp, not big shadows.

**Motion (see §10 too):** `--fs-ease-heat: cubic-bezier(.2,.7,.2,1)` (warm, weighted) ·
`--fs-dur-fast:120ms` `-base:200ms` `-slow:500ms`. Honor `prefers-reduced-motion` — drop all
non-essential motion; state still updates instantly.

---

## 6. Iconography

- Use a clean line-icon set with SF-Symbols character (e.g., Lucide via inline SVG, or Fluent
  System Icons). Stroke ~1.5–2px, sized 16/18/20. Color = `currentColor`, inheriting text/semantic.
- One icon family app-wide. No emoji in product chrome (emoji are fine only in playful release-note
  copy, never in functional UI).
- Status icons pair with text + color (never color alone) for accessibility.

---

## 7. App shell & navigation

**Sherpa-style grouped left sidebar** (it scales to many sections and matches our sibling app).

- **Sidebar:** translucent grouped surface (`--fs-surface-sidebar`). Grouped sections with an
  eyebrow label; each row = leading icon + label. **Active row:** copper left-indicator (3px) +
  `--fs-accent-text` label + subtle `--fs-accent` @ ~12% row tint. Hover = neutral tint.
  - v1 groups: **Serve** (M5, primary job — first) · **Discover** (Catalog) · **Workbench** (Chat) ·
    **Settings**. The rail leads with the primary job. Seed the structure now; disabled/"coming soon"
    rows are acceptable placeholders, clearly marked, never faking function.
- **Page header:** large leading glyph + `--fs-type-display` title + `--fs-type-subtitle`. (e.g.
  ⛏️-equivalent foundry glyph + "Browse models" + "Install, load, and safely manage Foundry Local
  models.")
- **Toolbar:** right-aligned utility row — search field (pill, `--fs-radius-pill`), filters,
  refresh, optional account/context. Respects the macOS traffic-light inset.
- **Content:** cards on `--fs-canvas`; list/detail or grouped lists; command-palette overlays for
  dense pick flows (model load/search) are encouraged (LM Studio pattern).

---

## 8. Component inventory

All components consume tokens; none hardcode hex. Keep existing `data-testid`/`id` hooks.

### Buttons
- **Primary (copper):** `--fs-accent-fill` bg, `--fs-text-inverse` label, `--fs-radius-md`.
  Reserved for the one primary action per view (Download, Load, Start server). Use plain labels (§9).
- **Secondary:** surface bg, `--fs-border-strong` border, `--fs-text` label.
- **Ghost:** transparent, text-only, for low-emphasis (Skip, tertiary nav).
- **Destructive:** `--fs-danger` — only inside the consent pattern (§11), never a bare list action.

### Chips & badges
- **Capability chip:** soft tinted pill + line icon + label (Vision / Tool Use / Reasoning). Tint is
  decorative-neutral or the relevant semantic; **never copper** (copper isn't a capability claim).
  Only render a capability the model actually advertises; otherwise omit or state the absence.
- **Format badge:** compact mono pill (ONNX). Neutral surface, `--fs-border`.
- **Status pill:** semantic — `Cached` (positive), `Loaded` (positive, stronger), `Warning`,
  `Danger`. Icon + text + color. A model `Cached` may get a small **copper stamp** as a brand
  moment, but the *status* meaning stays semantic.

### Cards & list/detail
- Card: `--fs-surface`, `--fs-border`, `--fs-radius-lg`, `--fs-space-5` padding, `--fs-elev-card`.
- Optional **left-accent border** for stateful rows (Sherpa device-card pattern): e.g. a loaded
  model gets a `--fs-positive` left edge; the *currently focused* model may get a quiet copper
  ember-gradient behind it ("heat under control").

### Dialog (consent) — see §11.

### Toolbar / search — see §7.

### Progress (honest)
- Determinate bar fills with `--fs-accent` ONLY while real bytes/load progress move; show the real
  numbers ("1.2 GB of 2.4 GB"). Before the first real datapoint, render an **indeterminate** state
  (shimmer or 0%) — never a full 100% bar. (M3 review learning, now a rule.)

### Empty / loading / error
- Empty: foundry mark + one calm line + the single primary action ("Choose your first model").
- Loading: name the *real* step if the SDK exposes it ("Loading model…"); never invent sub-steps.
- Error: `--fs-danger` text + plain message + a retry affordance. No blame, no fabrication.

### Metrics row (chat, M4)
- Mono, tertiary text: tokens/sec · token count · time · stop reason — straight from the engine.

### Composer (chat, M4 — design now, build later)
- Rounded multiline input on `--fs-surface`; leading attach/tool affordances; capability toggles as
  chips (only if the loaded model supports them); copper send; honest token/context indicator.

---

## 9. Content & voice

Short, calm, specific. Warm, never hypey.

**The plain-language rule (controls vs. flavor).** Anything the user must read to *operate* the app —
button labels, control labels, section headers, and the primary status word — uses plain, literal
language (Download, Load, Start server, Running, Stopped, Loaded). The forge metaphor is **ambient
flavor on real state**, never the operable label, and never a synonym you alternate with a plain one
in the same view. Concretely:

- ✅ Button: "Load model & start server". Status badge: "Running". A model chip: "Loaded".
- ✅ Forge flavor as *decoration* on true state: the copper pilot-light ember (motion, not a word);
  an optional secondary microcopy line; the product narrative in docs/marketing.
- ❌ A control or header a user must decode to act: "Cast · Temper · Serve", "Forge State",
  "Tempered" as the only label, "Light local server" competing with a "running" badge, "Primary Job"
  (internal JTBD jargon).
- If a forge word appears in operable UI at all, gloss it inline ("Load (temper)") — but prefer plain.

This supersedes the earlier "state language only" phrasing: forge vocabulary maps to real state **and**
stays out of the controls. Never mix two vocabularies for one state in one view (no "Forge lit" title
above a "running" badge — pick "Running").

Reference microcopy (FoundryForge voice) — plain is primary; forge is optional decoration:

| Context | Primary (plain — use this) | Optional forge flavor (decoration only) |
|---|---|---|
| Welcome | "Build with local models on this Mac." | — |
| First-run subtitle | "Download a model, load it into memory, then chat — local after the first download." | — |
| Empty primary action | "Choose your first model" | — |
| Download button | "Download" / "Download to cache" | "cast" (narrative only) |
| Download progress | "Downloading Phi-3.5 Mini — 1.2 of 2.4 GB" | — |
| Load button | "Load in memory" | "temper" (tooltip / narrative) |
| Loaded chip / toast | "Loaded" · "Phi-3.5 Mini is loaded and ready." | — |
| Chat placeholder | "Ask this local model…" | — |
| Start-server action | "Load model & start server" | — |
| Server-on status | "Running — `http://127.0.0.1:5273`" | copper pilot-light ember (motion only) |
| Server-off status | "Stopped" | — |
| Reachable model | "Loaded · `qwen2.5-…:4`" | — |
| Server limits | "Localhost only · No auth · No LAN access" | — |
| Capability honesty | "This model does not advertise vision support." | — |

**Honesty rules:** never say "compatible," "safe," "will fit," or "guaranteed JSON" unless the app
can prove it. **Never imply an integration the target tool does not actually support** — do not claim
a tool can point at the local endpoint unless we can show a real, working config for it (e.g. GitHub
Copilot's BYOK does not consume an arbitrary localhost OpenAI endpoint; do not suggest it does).
Disk-fit is advisory and per-variant (§11/M3). Surface unsupported Foundry Local capabilities as
plainly-stated limits, not dead UI.

---

## 10. Signature moments

Three designed "delight" moments — each inseparable from a real event and from transparency.

1. **First Cast** — after the first successful *download*, the model card gets a copper
   "cached locally" stamp and warms from neutral steel to copper edge-light, then settles. One
   restrained 500ms glow. No confetti. Celebrates a real fact: bytes are on disk.
2. **Tempering** — while *loading*, the card shows a narrow copper heat line L→R with status text
   naming the actual step ("Loading model…", or real sub-steps if exposed). Slow, weighted,
   industrial. Treats loading as a serious machine state, not magic.
3. **Forge Lit** — turning on the *server* ignites a copper pilot-light dot (soft ember pulse →
   steady) in a compact panel whose **status text reads plainly "Running"** (the ember is the only
   forge element — motion, not vocabulary). The panel *immediately* shows the exact bound URL and the
   loaded model(s) reachable at it. The delight is the transparency, not the metaphor.

Motion principles: (1) motion maps to real state; (2) heat eases slowly (`--fs-ease-heat`);
(3) one ember at a time — only one primary copper animation per view; (4) settle quickly into
stable, readable UI.

---

## 11. Data-preservation & destructive-action UX (Constitution §IV)

The model cache (multi-GB) and settings are protected user data. The destructive-action pattern:

1. The list/card affordance (e.g. delete) **only opens a consent dialog** — it never mutates.
2. The dialog **names the exact thing** and the consequence and irreversibility:
   *"Delete cached model 'qwen2.5-0.5b'? This frees 0.7 GB and cannot be undone."*
3. Dialog is `role="dialog"` + `aria-modal="true"`, focus-trapped, Esc-cancels, and **defaults
   focus to Cancel** (not the destructive button).
4. The destructive button uses `--fs-danger`; confirm calls the service with explicit
   `userConfirmed: true`. That call path exists **only** in the dialog's confirm handler.
5. Prefer non-destructive alternatives where possible (unload vs. delete; soft settings vs. wipe).

---

## 12. Accessibility

- WCAG 2.1 AA contrast for text and meaningful UI; verify every copper/semantic pairing.
- Full keyboard operability; visible focus (`--fs-accent-ring`, `outline-offset: 2px`).
- Color is never the sole signal — pair with icon + text.
- `prefers-reduced-motion`: disable signature animations; state still updates instantly.
- Correct ARIA: `role="progressbar"` + `aria-valuenow` on real progress; `aria-live` for async
  status; labeled controls; dialog semantics per §11.

---

## 13. Token reference (maps to `wwwroot/tokens.css`)

`tokens.css` defines every `--fs-*` token in `:root` (Workshop Daylight) and overrides them under
`@media (prefers-color-scheme: dark)` **and** `[data-theme="dark"]`/`[data-theme="light"]` for an
explicit in-app toggle. Components reference only `--fs-*` — no raw hex in component CSS. Adding a
color means adding a token here first. Theme parity is a review gate: any new surface must be
verified in **both** Workshop Daylight and Night Forge.

---

## 14. Governance

- This guide is referenced from `AGENTS.md` (Code conventions). New UI must conform.
- Where this guide and the Constitution touch (honesty, data preservation), the Constitution wins;
  this guide says how to *show* it.
- Changes are deliberate: update the guide first, then the tokens, then components. Verify both
  themes with frontmost screenshots (KI-001) before claiming done.

**Coding-agent anchor:** Fluent-2-structured, macOS-native shell; Sherpa-style grouped sidebar; SF
typography; warm neutral surfaces; exactly one brand accent — **Foundry Copper**. The forge
metaphor is state language and subtle material atmosphere, not decoration. Every beautiful moment
corresponds to a real local event: downloaded, loaded, streaming, or server bound.
