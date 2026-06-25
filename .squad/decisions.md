# Squad Decisions

## Active Decisions

### DEC-001 — Workflow: Squad + spec-kit cohabitation
**Date:** 2026-06-25 · **Status:** Active
**Decision:**
- **spec-kit owns planning:** `/speckit.constitution`, `/speckit.specify`, `/speckit.plan`, `/speckit.tasks`.
- **Squad owns the implementation team:** specialists, reviewer-independence, async triage, this decisions log.
- **Bridge:** `/speckit.tasks` output is the input to Squad's coordinator; specialists pick tasks; decisions append back here.
**Rationale:** Squad alone has no prescribed planning structure; spec-kit alone has no enforced reviewer separation. Together they cover Karpathy's six steps.

### DEC-002 — Canonical reference
**Date:** 2026-06-25 · **Status:** Active
**Decision:** The canonical plan is **`docs/PLAN.md`** in this repo (hardened after a skeptic review and validated against MAUI.Sherpa). Supporting research: `docs/research/`. AGENTS.md, KNOWN-ISSUES.md, and KNOWN-GOOD-VERSIONS.md are binding guardrails. Until a feature is specced via `/speckit.specify`, `docs/PLAN.md` is the source of truth.

### DEC-003 — Methodology
**Date:** 2026-06-25 · **Status:** Active
**Decision:** Karpathy's six-step pattern ([blog](https://karpathy.bearblog.dev/sequoia-ascent-2026/)) is the operational frame: (1) define context, (2) tools, (3) feedback loop, (4) guardrails, (5) let agents work, (6) preserve human understanding. Each major feature spec runs through all six.

### DEC-004 — Architecture & scope (FoundryStudio)
**Date:** 2026-06-25 · **Status:** Active
**Context:** Confirmed via owner decisions, a skeptic review, and validation against [Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa) (a working MAUI + Blazor Hybrid + macOS AppKit app on the same packages).
**Decision:**
- **Stack:** `net11.0-macos` AppKit head (maui-labs) + Blazor Hybrid; macOS / Apple Silicon only in v1.
- **Foundry Local:** in-process SDK for catalog/model/chat via a thin `IChatClient` adapter (no loopback socket); the local OpenAI server is exposed for **external** tools only; one `FoundryLocalManager` singleton with a load/unload concurrency gate.
- **Scope:** v1 is the lighthouse core **M0 -> M4 + the M5 server toggle**; RAG/voice/presets/MCP/i18n are post-v1.
- **M0 is the linchpin gate** (toolchain pin -> FL dylib-chain bundling/signing -> BlazorWebView probe -> vertical slice). Nothing proceeds to M1 until M0 is green. Human-in-the-loop, not autonomous.
- **Capability honesty:** never ship UI for unsupported FL features (server auth/LAN, GGUF import, top_k/seed). Pin a known-good version set; upgrades re-run M0.
**Rationale:** Mac Catalyst can't load FL's `osx-arm64` dylib; AppKit can. Sherpa proves the stack + the native-bundling MSBuild pattern + hardened-runtime entitlements. See `docs/PLAN.md`.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
