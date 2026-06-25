# FoundryStudio

A desktop client for Microsoft Foundry Local: a native macOS (AppKit) .NET MAUI app with a Blazor Hybrid UI, also exposing Foundry Local's local OpenAI-compatible server. LM Studio is the feature reference.

**Canonical plan:** [`docs/PLAN.md`](docs/PLAN.md) — hardened after a skeptic review and validated against [Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa). Read it before contributing. Guardrails: [`AGENTS.md`](AGENTS.md), [`KNOWN-GOOD-VERSIONS.md`](KNOWN-GOOD-VERSIONS.md), [`KNOWN-ISSUES.md`](KNOWN-ISSUES.md).

## How we work

This repo uses a layered agent workflow:

- **[spec-kit](https://github.com/github/spec-kit)** owns planning. Slash commands: `/speckit.constitution`, `/speckit.specify`, `/speckit.plan`, `/speckit.tasks`, `/speckit.clarify`, `/speckit.implement`.
- **[Squad](https://github.com/bradygaster/squad)** owns the implementation team. Roster in `.squad/team.md`; decisions ratify in `.squad/decisions.md`.
- **[Karpathy's six-step methodology](https://karpathy.bearblog.dev/sequoia-ascent-2026/)** is the operational frame for every feature: context, tools, feedback loop, guardrails, agents work, human understanding.

See `.specify/memory/constitution.md` for governing principles (run `/speckit.constitution` to generate it) and `.squad/decisions.md` for the running decision log.

## Team

| Name | Role | Charter |
|------|------|---------|
| Ripley | Lead / Strategy | `.squad/agents/ripley/charter.md` |
| Bishop | Foundry Local Integration | `.squad/agents/bishop/charter.md` |
| Hicks | Blazor UI | `.squad/agents/hicks/charter.md` |
| Vasquez | Native & Packaging | `.squad/agents/vasquez/charter.md` |
| Drake | Test & CI | `.squad/agents/drake/charter.md` |
| Spunkmeyer | PR Quality | `.squad/agents/spunkmeyer/charter.md` |
| Scribe / Ralph / Rai | Built-in: memory, history, responsible AI | `.squad/agents/` |

## Architecture at a glance

- **net11.0-macos AppKit head** (maui-labs `Microsoft.Maui.Platforms.MacOS`) — not Mac Catalyst.
- **Blazor Hybrid** UI in BlazorWebView; components run in-process.
- **In-process Foundry Local SDK** for catalog/model/chat via a thin `IChatClient` adapter; the local OpenAI server is exposed for external tools only.
- **v1 = lighthouse core:** M0 (feasibility gates) to M4 (chat) plus the M5 server toggle. RAG/voice/presets/MCP are post-v1.

## Getting started (for contributors)

1. Install Squad CLI: `npm install -g @bradygaster/squad-cli@latest`
2. Install spec-kit: `uv tool install specify-cli --from "git+https://github.com/github/spec-kit.git@$(curl -fsSL https://api.github.com/repos/github/spec-kit/releases/latest | grep tag_name | head -1 | sed -E 's/.*"tag_name": *"([^"]+)".*/\1/')"`
3. Read `docs/PLAN.md`, `AGENTS.md`, and `.squad/team.md`.
4. From this directory: `copilot --agent squad`
5. For a new feature: `/speckit.specify <description>` then `/speckit.plan` then `/speckit.tasks`. Squad's coordinator picks up `tasks.md`.

## Status

Pre-alpha. **No app code yet** — the next step is the M0 feasibility gate (prove the Foundry Local native dylib chain bundles and loads on the net11 AppKit head). Nothing proceeds to M1 until M0 is green. Squad and spec-kit are at 0.x; run `squad upgrade --self` and `specify self upgrade` to stay current.
