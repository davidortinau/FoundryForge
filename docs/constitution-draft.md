# FoundryStudio Constitution (draft body)

Run `/speckit.constitution` in a Copilot session from this repo to generate `.specify/memory/constitution.md`. The slash command reads repo context (this draft, `docs/PLAN.md`, `.squad/decisions.md`, `AGENTS.md`). Use the body below as the input.

---

Establish governing principles for FoundryStudio: a desktop client for Microsoft Foundry Local — a native macOS (AppKit) .NET MAUI app with a Blazor Hybrid UI, also exposing Foundry Local's local OpenAI server.

OUTCOMES (engineering culture):
- Always retry and fix the root cause — no band-aids, no silent timeouts. Surface the diagnosed cause, not a generic failure.
- An engineer should never see a red X on CI. CI failures are a project failure mode, not a contributor task.

METHODOLOGY (Karpathy's six steps — apply to every feature):
1. Define the context — research, references, prior art loaded up front.
2. Define the tools — agents, MCP servers, APIs the work will use.
3. Define the feedback loop — the evaluator that judges success (here: MAUI DevFlow end-to-end on real Apple Silicon).
4. Define the guardrails — accessibility, security, privacy, capability honesty.
5. Let agents work — bounded retries, parallel where independent.
6. Preserve human understanding — decisions captured in .squad/decisions.md, audit trail intact.

DESIGN TENETS:
- Citation before action — every architectural claim cites a source (file, commit, doc URL, or measured result). "I think" plus a slow loop equals stop and read.
- Pre-completion verification — never claim done without an end-to-end check appropriate to the change. Build success is a prerequisite, not verification. UI-bearing changes verify via MAUI DevFlow.
- Surgical changes — every changed line traces to the request; no opportunistic refactoring; clean up only your own mess.
- Reviewer independence — the agent that wrote the code cannot approve it. Squad's enforced handoffs implement this.
- Data preservation — the model cache and settings are user data; never wipe, drop, uninstall, or rewrite history without explicit per-turn consent.
- Native-load discipline — the Foundry Local dylib chain bundling and signing on the AppKit head is the linchpin; prove the M0 gate before building features.
- In-process first — chat runs through an in-process IChatClient adapter, not a loopback socket; the exposed local server is for external tools only; one FoundryLocalManager singleton with a load/unload concurrency gate.
- Capability honesty — never ship UI for capabilities Foundry Local does not have (server auth, LAN bind, GGUF import, top_k/min_p/seed, speculative decoding). Surface limits; do not fake.
- Pinned-preview discipline — freeze a known-good version set; upgrades are deliberate chores that re-run the M0 gate.
- Lighthouse scope — v1 is M0 to M4 plus the M5 server toggle; macOS / Apple Silicon only; resist scope creep into post-v1 (RAG, voice, presets, MCP, i18n).

QUALITY GATES:
- WCAG AA accessibility by default.
- All persistent memory (model cache, settings, chat history) writes are auditable and user-editable; never wiped without consent.
- Every milestone closes with a real Apple-Silicon DevFlow end-to-end check (download, load, streamed reply; for the server, an external curl), plus a Verified line.
- Telemetry follows OpenTelemetry gen AI semantic conventions; no PII in logs.
- Every workaround for an upstream gap links its tracking issue in KNOWN-ISSUES.md and is removed when the fix lands.

REVIEW PROTOCOL:
- Squad's coordinator routes work to specialists; specialists draft and stage; Test & CI runs the harness; PR Quality enforces the guardrails; the original author cannot self-approve.
- Spec-kit's /speckit.tasks output is the input to Squad's coordinator.
- docs/PLAN.md is the canonical reference until a feature is ratified into a repo-local spec via /speckit.specify; decisions append to .squad/decisions.md.
