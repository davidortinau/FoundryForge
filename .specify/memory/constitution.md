<!--
Sync Impact Report
==================
Version change: TEMPLATE → 1.0.0
Bump rationale: Initial ratification of the FoundryForge constitution from
  docs/constitution-draft.md. All template placeholders replaced with concrete
  governing principles; first formal version.

Modified principles:
  - [PRINCIPLE_1_NAME] → I. Citation Before Action
  - [PRINCIPLE_2_NAME] → II. Pre-Completion Verification (NON-NEGOTIABLE)
  - [PRINCIPLE_3_NAME] → III. Surgical Changes & Reviewer Independence
  - [PRINCIPLE_4_NAME] → IV. Data Preservation & Capability Honesty
  - [PRINCIPLE_5_NAME] → V. Native-Load & In-Process Discipline

Added sections:
  - Scope & Quality Gates (was [SECTION_2_NAME])
  - Development Workflow & Review Protocol (was [SECTION_3_NAME])

Removed sections: none

Templates requiring updates:
  - .specify/templates/plan-template.md ✅ generic Constitution Check, no change needed
  - .specify/templates/spec-template.md ✅ no principle-specific references
  - .specify/templates/tasks-template.md ✅ no principle-specific references

Follow-up TODOs: none
-->

# FoundryForge Constitution

FoundryForge is a desktop client for Microsoft Foundry Local: a native macOS (AppKit)
.NET MAUI app with a Blazor Hybrid UI that also exposes Foundry Local's local
OpenAI-compatible server. These principles are non-negotiable and govern every feature.

## Engineering Culture

- Always retry and fix the root cause. No band-aids, no silent timeouts. Surface the
  diagnosed cause, not a generic failure.
- An engineer MUST never see a red X on CI. CI failures are a project failure mode, not a
  contributor task.

Every feature follows the six-step method: (1) define the context — research, references,
and prior art loaded up front; (2) define the tools — agents, MCP servers, and APIs the
work will use; (3) define the feedback loop — the evaluator that judges success, here MAUI
DevFlow end-to-end on real Apple Silicon; (4) define the guardrails — accessibility,
security, privacy, capability honesty; (5) let agents work — bounded retries, parallel
where independent; (6) preserve human understanding — decisions captured in
`.squad/decisions.md` with the audit trail intact.

## Core Principles

### I. Citation Before Action

Every architectural claim MUST cite a source: a file, a commit, a documentation URL, or a
measured result. "I think" combined with a slow feedback loop means stop and read first.
Guessing is permitted only when the feedback loop is cheap (≤5s) and the search space being
narrowed can be named; cap such guesses at three tries before reading the source.

Rationale: invented confidence rationalizes whichever direction is convenient. A citation is
auditable; a hunch is not.

### II. Pre-Completion Verification (NON-NEGOTIABLE)

No work is claimed done without an end-to-end check appropriate to the change. Build success
is a prerequisite, not verification. UI-bearing changes MUST be verified via MAUI DevFlow on
a real Apple Silicon Mac (download → load → streamed reply; for the exposed server, an
external `curl`). Every milestone-closing note MUST end with a `Verified:` line naming the
checks that ran. A missing `Verified:` line means the work is incomplete by definition.

Rationale: "the build passed and unit tests are green" is the exact failure mode this gate
exists to prevent.

### III. Surgical Changes & Reviewer Independence

Every changed line MUST trace to the request. No opportunistic refactoring; clean up only
your own mess. The agent that wrote the code MUST NOT approve it — reviewer independence is
enforced through Squad's mandatory handoffs.

Rationale: scoped diffs are reviewable and revertible; self-approval defeats review.

### IV. Data Preservation & Capability Honesty

The model cache (multi-GB), settings, and chat history are user data. They MUST NOT be wiped,
dropped, uninstalled, or rewritten without explicit per-turn consent; back up before any
destructive operation and prefer a settings toggle over deletion. FoundryForge MUST NOT ship
UI for capabilities Foundry Local does not have — server auth, LAN bind, GGUF import,
`top_k`/`min_p`/`seed`, speculative decoding. Surface such limits honestly; never fake them.

Rationale: destroying user data and shipping dead UI both break user trust irreversibly.

### V. Native-Load & In-Process Discipline

The Foundry Local dylib chain — bundled and signed on the AppKit head — is the linchpin; the
M0 native-load gate MUST be proven before building features. Chat MUST run through an
in-process `IChatClient` adapter, not a loopback socket; the exposed local OpenAI server is
for external tools only. There MUST be exactly one `FoundryLocalManager` singleton, guarded by
a load/unload concurrency gate. The known-good version set is pinned; upgrades are deliberate
chores that re-run the M0 gate.

Rationale: if the native chain does not load and sign, nothing else matters; a second manager
or a loopback hop reintroduces the failures this architecture was chosen to avoid.

## Scope & Quality Gates

Scope: v1 is the lighthouse core, milestones M0 through M4 plus the M5 server toggle, macOS /
Apple Silicon only. Post-v1 work (RAG, voice, presets, MCP host, i18n) is out of scope; resist
scope creep. Models are ONNX-only.

Quality gates that MUST hold:

- WCAG AA accessibility by default.
- All persistent memory writes (model cache, settings, chat history) are auditable and
  user-editable, and are never wiped without consent.
- Every milestone closes with a real Apple-Silicon DevFlow end-to-end check and a `Verified:`
  line.
- Telemetry follows OpenTelemetry generative-AI semantic conventions; no PII in logs.
- Every workaround for an upstream gap links its tracking issue in `KNOWN-ISSUES.md` and is
  removed when the fix lands.

## Development Workflow & Review Protocol

This repo combines Squad (`.squad/`) and spec-kit (`.specify/`). Spec-kit's
`/speckit.tasks` output is the input to Squad's coordinator. The coordinator routes work to
specialists; specialists draft and stage; Test & CI runs the harness; PR Quality enforces the
guardrails. The original author cannot self-approve (see Principle III).

`docs/PLAN.md` is the canonical reference until a feature is ratified into a repo-local spec via
`/speckit.specify`. Decisions append to `.squad/decisions.md`. Run `/review` before any push.

## Governance

This constitution supersedes other practices where they conflict. Amendments require a written
proposal, review and approval through the standard Squad/PR Quality handoff, and a recorded
entry in `.squad/decisions.md`.

Versioning follows semantic rules: MAJOR for backward-incompatible governance or principle
removals/redefinitions; MINOR for a new principle or materially expanded guidance; PATCH for
clarifications and non-semantic refinements.

Compliance is verified at review time: every plan's Constitution Check and every PR review MUST
confirm adherence to these principles, and any justified deviation MUST be documented in the
plan's Complexity Tracking with explicit rationale.

**Version**: 1.0.0 | **Ratified**: 2026-06-24 | **Last Amended**: 2026-06-24
