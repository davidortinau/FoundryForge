# AGENTS.md — FoundryStudio

Guidance for every agent (and human) working in this repo. Keep it short and obeyed. Canonical detail lives in [`docs/PLAN.md`](docs/PLAN.md).

## What this is
A desktop client for Microsoft Foundry Local: a native macOS (AppKit) .NET MAUI app with a Blazor Hybrid UI, also exposing Foundry Local's local OpenAI-compatible server. LM Studio is the feature reference.

## Non-negotiable architecture
- **AppKit head** (`Microsoft.Maui.Platforms.MacOS`, from dotnet/maui-labs) on **`net11.0-macos`** — never Mac Catalyst (its RID can't load Foundry Local's `osx-arm64` dylib).
- **Blazor Hybrid** UI (BlazorWebView), components run in-process (no server circuit, no `HttpContext`).
- **In-process Foundry Local SDK** for catalog/model/chat, wrapped in a thin `IChatClient` adapter so MEAI middleware composes **without a loopback socket**.
- **One `FoundryLocalManager` singleton** backs both the UI and the exposed local server. Never construct a second.
- The **exposed local OpenAI server is for external tools only** (it's the LM Studio "server" feature); our own chat does not go through it.

## Build / run / verify (mandatory loop)
- Build/run with `dotnet build -t:Run`. Every running-app change goes through **MAUI DevFlow** (`maui-devflow-debug`).
- Read native logs to diagnose; **never uninstall or wipe to "reset."**
- An **end-to-end check on a real Apple Silicon Mac** (download -> load -> streamed reply; server -> external `curl`) precedes any "done." Build success is a prerequisite, not verification. End milestone-closing notes with a `Verified:` line.
- Run `/review` before any push. Reviewer independence: the agent that wrote the code cannot approve it.

## UI Definition of Done (design gate)
Any change that alters a rendered surface is **not** done until this checklist passes. This is a hard gate — the same weight as the pre-push `/review` gate — added because too many avoidable UI defects (asymmetric margins, dead controls, regions not reaching the viewport, mock mismatches) reached review instead of being caught here. "Build succeeds and it looks plausible in one screenshot" is **not** verification.
1. **Mock parity (if a mock/reference exists).** Run the **`maui-visual-review`** skill (screenshot vs the provided mock) and resolve every discrepancy — margins, alignment, spacing, right/left symmetry, and "reduce to the varying info." A mock in the task description is a spec, not a suggestion.
2. **Principle + honesty sweep.** Run the **`design-review`** agent proactively. Zero **dead affordances** — every control that looks interactive does something; never render UI for a capability Foundry Local doesn't have (Constitution honesty rule).
3. **Exercise every new control.** Click/type/toggle each new interactive element and confirm it has an effect. Confirm every "close/dismiss" affordance fires from **every** empty region a user would try (not just one sub-area).
4. **Bounds check.** Every scroll region and side panel reaches the bound it implies (full height to the window edge / dock, no dead gutter, no short-stop that reads as "that's all").
5. **Resize test.** Drag narrow → wide; confirm adaptive collapse/expand and that nothing overlaps or strands (this is the desktop "resize resilience" principle — actually test it, don't assume it).
6. **Both themes.** Verify in Workshop Daylight **and** Night Forge (frontmost screenshots, KI-001), then restore theme to System.

End UI-change closing notes with a `Verified:` line naming which of these ran.

## Scope boundaries (stay focused)
- **macOS / Apple Silicon only in v1.** No iOS/Android/Mac Catalyst code paths or RIDs.
- **v1 = lighthouse core: M0 -> M4 + the M5 server toggle.** RAG, voice, presets, MCP host, i18n are **post-v1**. Resist scope creep.
- Models are **ONNX-only** (no GGUF/safetensors import).
- **Do not fake unsupported Foundry Local capabilities** — server auth, LAN bind, `top_k`/`min_p`/`seed`, speculative decoding don't exist in FL. Surface them as limits; don't ship dead UI.

## Data preservation
The model cache (multi-GB) and settings are user data. Never wipe to reset; back up before any destructive cache op; prefer a settings toggle over deletion. Per-turn consent before anything destructive.

## Dependency discipline
- Pin the Foundry Local package (`sdk` vs `sdk_v2` — confirm which is current, record it). Pin the maui-labs AppKit set per [`KNOWN-GOOD-VERSIONS.md`](KNOWN-GOOD-VERSIONS.md). Do not float to "latest"; upgrades are deliberate chores that re-run the M0 gate.
- When an upstream falls short: **fix what David owns** (maui-labs AppKit, Comet, DevFlow) directly and dogfood via LocalNuGets; **workaround + file an issue** for Foundry Local (route to Maanav Dalal / Meng Tang); **shim + pin** other upstreams. Record every workaround in [`KNOWN-ISSUES.md`](KNOWN-ISSUES.md) with its tracking link; remove on fix.

## Known constraints
- `FoundryLocalManager.CreateAsync()` is async; gate the UI on a `ReadyAsync()` ready-state; **no `.Result`/`.Wait()`** on init.
- Implement the singleton **load/unload concurrency gate** (drain or reject in-flight generations) before exposing the server.
- First run needs network to download models/EPs; fully offline after.

## Code conventions
- C# nullable enabled; services via the MAUI DI container; Razor components call injected services directly.
- Keep platform-specific code out of shared UI; follow the MAUI.Sherpa project shape (see `docs/PLAN.md` -> Reference architecture).
- **UI follows the Design Guide ([`docs/DESIGN.md`](docs/DESIGN.md)).** Every surface uses the `--fs-*` design tokens (no hardcoded hex), the Foundry Copper accent, Workshop Daylight + Night Forge themes at parity, the Sherpa-style sidebar shell, and the honesty/consent UX patterns. Verify new UI in **both** themes (frontmost screenshots, KI-001) before "done." Where the guide and Constitution touch (honesty, data preservation), the Constitution wins.

## Workflow
This repo uses Squad (`.squad/`) + spec-kit (`.specify/`). Plan with `/speckit.*`; Squad's coordinator routes `tasks.md` to the specialists in `.squad/team.md`. Decisions ratify in `.squad/decisions.md`.
