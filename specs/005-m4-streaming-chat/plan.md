# Implementation Plan: M4 — Chat experience (v1 core)

**Branch**: `005-m4-streaming-chat` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-m4-streaming-chat/spec.md`

## Summary

M4 activates the sidebar **Chat** nav (today a disabled "Coming soon" placeholder) and ships the v1 lighthouse payoff: a multi-turn, **streaming**, **in-process** conversation with a model the user loaded via M3. Chat runs entirely through the existing `IChatService.StreamAsync(IEnumerable<ChatMessage>, ChatOptions?, CancellationToken) → IAsyncEnumerable<ChatResponseUpdate>` over `FoundryChatClient` (MEAI) — **no loopback socket** (the M5 server is for external tools only). The milestone delivers: incremental token rendering with markdown + code-copy (US1); a loaded-model gate with load-on-demand through the M1 `IModelStateGate` (US2); cancellable streaming (US3); honest token metrics from the real stream (US4); a per-chat system prompt and the **only four** FL-supported inference params — temperature, max tokens, top_p, frequency_penalty (US5); an explicitly-**estimated** context-window tracker (US6); persisted, consent-gated chat history (US7, the Constitution IV load-bearing story); and real tool/function calling via MEAI `UseFunctionInvocation()` with one or two genuine .NET tools (US8). Best-effort structured output and regenerate-last are optional P3 (US9) and do not gate the milestone.

**Technical approach**: M4 is a **UI + pure-logic-seam** milestone built on the already-documented MEAI pipeline seam in `ChatService` (`adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()`). New work lands as: (a) **Core pure seams** (FL-free, dylib-free, unit-tested in the Core-only test project) — `TranscriptAssembler`, `InferenceParameters`→`ChatOptions` mapping, `TokenStatsAccumulator`, `ContextWindowEstimator`, `ChatMarkdown` (Markdig render + sanitize + code-block extraction), `ChatHistoryDocument` (serialization, mirrors `SettingsDocument`), and a new `IChatHistoryStore` abstraction with a file-based `FileChatHistoryStore` impl (human-readable JSON in app data, consent-gated, `.bak` non-destructive recovery — mirrors `FileSettingsService`; the App injects the path); (b) **Foundry-layer wiring** — build the MEAI middleware pipeline in `ChatService`, map `ChatOptions` (the four real params + tools, best-effort `response_format`) into the FL request, and surface real usage/finish-reason from the stream in `FoundryChatClient`/`FoundryMessageMapper`, plus one or two genuine app-defined `AIFunction` tools; (c) **Blazor UI** in `.App` (consuming only `IChatService` + Core seams + the M1–M3 services — never FL types) — a `Chat.razor` surface with composer, streamed message list, metrics row, context meter, params/system-prompt panel, conversation list, and the reused M3 `ConfirmDialog` for clear/delete, behind an activated `nav-chat`. Verification follows Constitution II: dylib-free unit tests for every seam (incl. the consent gate and a fake-stream tool-invocation test) + a real Apple-Silicon DevFlow DOM e2e whose only live destructive action targets a conversation the test itself created (FR-040), leaving pre-existing user history untouched.

## Technical Context

**Language/Version**: C# 13 / .NET 11 (`net11.0-macos` for `.App`; `net11.0` for `.Core`/`.Foundry`/`.Tests`), pinned via `global.json` / `Directory.Packages.props`. (Supersedes any net10 boilerplate — the net10 Sherpa baseline in `Directory.Packages.props` is `Condition=false` reference-only.)

**Primary Dependencies**: .NET MAUI Blazor Hybrid (AppKit head); Microsoft.Extensions.AI `10.0.1` (already referenced — `ChatMessage`/`ChatOptions`/`ChatResponseUpdate`/`ChatRole`/`AIFunctionFactory`/`UseFunctionInvocation`/`UseOpenTelemetry`); **Markdig `0.44.0`** (already pinned in `Directory.Packages.props`; add a `PackageReference` to `.Core` and refresh the lock file — research R1); Microsoft.AI.Foundry.Local `1.2.3` (behind the Foundry layer only); Betalgo.Ranul.OpenAI `9.1.0` (isolated to `FoundryMessageMapper`); Microsoft.Maui.DevFlow `0.25.0-dev` for DOM verification (KI-001/002).

**Storage**: **NEW** persistent store — chat history as human-readable JSON under `FileSystem.AppDataDirectory` (e.g. `<AppData>/chats/`), one file per conversation plus serialization owned by Core (`ChatHistoryDocument`), file IO by `FileChatHistoryStore`. This is **protected user data** (Constitution IV): consent-gated clear/delete, `.bak` non-destructive recovery on parse failure. No change to the model cache or `settings.json`.

**Testing**: xUnit in `tests/FoundryStudio.Tests` (Core-only, dylib-free) — unit tests for all Core seams, the history round-trip, the consent gate, the params→`ChatOptions` mapping, and a tool-invocation wiring test (fake `IChatClient` through `UseFunctionInvocation()` over a scripted stream). MAUI DevFlow DOM inspection on real Apple Silicon for UI verification (KI-001 sanctioned evidence path).

**Target Platform**: macOS / Apple Silicon only. No iOS/Android/Mac Catalyst. ONNX-only models.

**Project Type**: Desktop app — the existing 4-project MAUI Blazor Hybrid solution (App, Core, Foundry, Tests). **No new projects.**

**Performance Goals**: Tokens render incrementally as real `ChatResponseUpdate`s arrive; the stream is consumed with `await foreach` off the dispatcher and re-rendered via `await InvokeAsync(StateHasChanged)`, never blocking the BlazorWebView dispatcher (KI-005). First-token latency is *measured*, never animated. Cancellation halts promptly and cooperatively.

**Constraints**: **Honesty (Constitution III/IV)** — every token/metric comes from the real stream; genuinely-absent values render honest "unknown"/"estimate"; **only** temperature/max-tokens/top_p/frequency_penalty are surfaced (no `top_k`/`min_p`/`repeat_penalty`/`seed`); structured output is best-effort pass-through with no enforcement claim/toggle; tools must be real (no dead UI). **Data preservation (Constitution IV)** — history persists and survives restart; clear/delete only behind the named consent dialog (Cancel default-focus, "cannot be undone"). **Layering (Constitution V)** — the chat UI consumes only `IChatService` + Core seams + M1–M3 services, never the FL SDK. **In-process (Constitution V)** — `StreamAsync` over `FoundryChatClient`, no loopback; every stream takes a generation lease from the single `IModelStateGate`. **KI-005** — no `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` anywhere in the chat path; the `NoBlockingInitGuard` test stays green. **Accessibility** — WCAG AA on every new control in both Workshop Daylight and Night Forge themes.

**Scale/Scope**: 8 in-scope user stories (4×P1, 4×P2) + 1 optional P3; ~8 new Core seams (incl. the new `IChatHistoryStore` + file impl), ~9 new/extended Blazor components + 1 new page + sidebar nav activation, MEAI pipeline + options/usage/tools wiring in the Foundry layer, ~8 new unit-test classes. No new service projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Evidence / How M4 complies |
|-----------|--------|----------------------------|
| **I. Citation Before Action** | **PASS** | Every design claim cites real code (`IChatService.cs`; `ChatService.cs` pipeline seam comment `adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()`; `FoundryChatClient.cs` L43-72 stream + generation lease + the `TODO(M4)` options-mapping point; `FoundryMessageMapper.cs` token/empty-Choices guard; `FileSettingsService.cs`/`SettingsDocument.cs` persistence precedent; `ConfirmDialog.razor` + `ConsentGuard.cs` consent precedent; `ModelStateGate.cs` + `ModelBusyException`; `ModelInfo.ContextLength`), the MEAI 10.0.1 surface (`UsageContent`, `ChatFinishReason`, `AIFunctionFactory`, `UseFunctionInvocation`/`UseOpenTelemetry`), `docs/DESIGN.md` §8/§9/§11, the M0d finding (FL honors `tools`; `response_format` accepted-but-not-enforced), and `KNOWN-ISSUES.md` (KI-001/005/007). Open runtime unknowns (FL usage/finish-reason emission, FL cancel honoring, tool round-trip on hardware) are flagged in research.md and pinned for the DevFlow e2e — not guessed. |
| **II. Pre-Completion Verification (NON-NEGOTIABLE)** | **PASS (planned)** | quickstart.md defines a real Apple-Silicon DevFlow DOM e2e (load → multi-turn streaming → Stop → honest metrics → restart-reload → consent-gated delete on a test-created conversation) + dylib-free unit tests for every seam, the history round-trip, the consent gate, the params mapping, and tool-invocation wiring. M4 closes with a `Verified:` line; reviewer independent of author (FR-039). DOM is the sanctioned autonomous evidence path (KI-001). |
| **III. Surgical Changes & Reviewer Independence** | **PASS** | Scope is additive: new Core seams + new `IChatHistoryStore`, new/extended Blazor chat UI, and bounded pipeline/options/usage/tools wiring in the Foundry layer (the `ChatService` seam and `FoundryChatClient` `TODO(M4)` were left explicitly for this milestone). No opportunistic refactor of M1–M3 code; the sidebar `nav-chat` activation is mandated by FR-001. Author does not self-approve. |
| **IV. Data Preservation & Capability Honesty** | **PASS (load-bearing)** | Chat history is user data: persisted to app data, survives restart (FR-023/027); clear/delete only through the named `ConfirmDialog` (names the conversation, "cannot be undone", **Cancel** default-focus) — a single activation never deletes (FR-025/026), unit-proven dylib-free via the consent gate (SC-009); parse-failure recovery preserves `.bak` (no silent wipe). Capability honesty: real tokens/metrics only, honest "unknown" when usage/context absent (FR-016/022); **only four** real params, zero controls for `top_k`/`min_p`/`repeat_penalty`/`seed` (FR-019); structured output best-effort with no enforcement claim/toggle (FR-031); tools are genuine, no dead UI (FR-030). Verification's only live delete targets a test-created conversation (FR-040). |
| **V. Native-Load & In-Process Discipline** | **PASS** | The chat UI consumes only `IChatService` + Core seams + M1–M3 abstractions; all FL types stay behind the Foundry layer (FR-034). Chat is in-process via `FoundryChatClient` — **no loopback** (FR-002). Every stream takes a generation lease from the single `IModelStateGate` so a concurrent load/unload drains/rejects rather than tearing the stream; `ModelBusyException` is surfaced honestly (FR-010). Streaming/cancellation are fully async off the dispatcher, no `.Result`/`.Wait()` (KI-005, FR-037). |

**Initial gate**: ✅ PASS (no violations). **Post-Design re-check (Phase 1)**: ✅ PASS — the one additive abstraction (`IChatHistoryStore`) and the new persistent store are Constitution-IV-aligned user-data handling that mirrors the existing `ISettingsService`/`FileSettingsService` pattern, not a new service layer or an FL leak; the MEAI pipeline is built at the existing documented seam. No Complexity Tracking entry required.

## Project Structure

### Documentation (this feature)

```text
specs/005-m4-streaming-chat/
├── plan.md              # This file
├── research.md          # Phase 0 — markdown lib, usage/finish-reason extraction, persistence format, tool examples, pipeline build, cancellation, context estimate, evidence
├── data-model.md        # Phase 1 — ChatSession, ChatMessageRecord, InferenceParameters, GenerationMetrics, ContextWindowEstimate, IChatHistoryStore contract, ChatHistoryDocument
├── quickstart.md        # Phase 1 — dylib-free unit tests + DevFlow DOM e2e + data-preservation-safe verification plan
├── contracts/           # Phase 1
│   ├── core-seams.md             # Exact signatures: TranscriptAssembler, InferenceParameters→ChatOptions, TokenStatsAccumulator, ContextWindowEstimator, ChatMarkdown, ChatHistoryDocument, IChatHistoryStore/FileChatHistoryStore (+ consent gate)
│   ├── service-surface.md        # IChatService relied on + ChatService pipeline build + FoundryChatClient options/usage/tools wiring + tool registration
│   └── chat-ui.dom.md            # DOM id/data-testid hooks + honesty/consent/accessibility invariants for DevFlow
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root) — extend the existing 4 projects, no new projects

```text
src/FoundryStudio.Core/                       # FL-free, dylib-free (references Microsoft.Extensions.AI + Markdig)
├── Abstractions/
│   ├── IChatService.cs            # (existing; unchanged — StreamAsync seam)
│   └── IChatHistoryStore.cs       # NEW — persistence abstraction (list/load/save/delete; consent-gated delete)
├── Chat/
│   ├── TranscriptAssembler.cs     # NEW — ChatSession → IEnumerable<ChatMessage> (system + ordered turns) for the request
│   ├── InferenceParameters.cs     # NEW — record { Temperature, MaxOutputTokens, TopP, FrequencyPenalty } + ToChatOptions(modelId, tools)
│   ├── TokenStatsAccumulator.cs   # NEW — TTFT/tok-per-sec/total/stop-reason from stream timing + UsageContent; honest "unknown"
│   ├── ContextWindowEstimator.cs  # NEW — approx tokens vs ModelInfo.ContextLength → estimate + warn threshold + unknown case
│   ├── ChatMarkdown.cs            # NEW — Markdig render (DisableHtml/sanitize) + fenced-code-block extraction for copy
│   ├── ChatHistoryDocument.cs     # NEW — (de)serialize ChatSession JSON (human-readable; mirrors SettingsDocument)
│   └── FileChatHistoryStore.cs    # NEW — IChatHistoryStore file impl (App injects path; consent-gated; .bak recovery)
├── Models/
│   ├── ChatSession.cs             # NEW — record: Id, Title, SystemPrompt, InferenceParameters, ModelAlias, Messages, Created/Updated
│   ├── ChatMessageRecord.cs       # NEW — record: Role, Content, CreatedAt, optional GenerationMetrics, StopReason
│   ├── GenerationMetrics.cs       # NEW — record: Ttft?, TokensPerSec?, TotalTokens?, StopReason (each nullable = honest "unknown")
│   ├── ContextWindowEstimate.cs   # NEW — record: UsedTokensEstimate, ContextLength?, Fraction?, IsWarn, IsUnknown
│   └── ModelInfo.cs               # (existing; consume ContextLength — no schema change)
├── Catalog/ConsentGuard.cs        # (existing; reused for clear/delete consent)
└── Settings/SettingsDocument.cs   # (existing; persistence/serialization precedent)

src/FoundryStudio.Foundry/                    # FL behind the seam
├── ChatService.cs                 # CHANGED — build adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build(); register app tools
├── FoundryChatClient.cs           # CHANGED — map ChatOptions (4 params + tools + best-effort response_format) into request; emit UsageContent + FinishReason from the stream
├── Internal/FoundryMessageMapper.cs  # CHANGED — options→request mapping; extract usage/finish-reason; tool-call passthrough (Betalgo isolated here)
└── Tools/ChatTools.cs             # NEW — 1–2 genuine app-defined AIFunctions (e.g. get_current_time, get_loaded_model_info via IFoundryCatalogService)

src/FoundryStudio.App/
├── Components/
│   ├── Layout/Sidebar.razor       # CHANGED — activate nav-chat (NavLink → /chat); remove disabled "Coming soon"
│   ├── Pages/Chat.razor           # NEW — route /chat; orchestrates the chat surface; gate + load-on-demand
│   ├── Chat/
│   │   ├── Composer.razor         # NEW — multiline input ("Ask this local model…"), copper send, Stop while streaming
│   │   ├── MessageList.razor      # NEW — ordered turns; incremental assistant render off-dispatcher
│   │   ├── MessageBubble.razor    # NEW — markdown via ChatMarkdown; code blocks + Copy control
│   │   ├── MetricsRow.razor       # NEW — TTFT · tok/sec · total · stop reason (mono, tertiary); honest "unknown"
│   │   ├── ContextMeter.razor     # NEW — estimate-labeled usage vs context length; warn; unknown case
│   │   ├── InferenceParamsPanel.razor  # NEW — ONLY temperature/max-tokens/top_p/frequency_penalty
│   │   ├── SystemPromptEditor.razor    # NEW — per-chat system prompt
│   │   ├── ConversationList.razor # NEW — list/select/new/duplicate/clear/delete
│   │   └── ChatGate.razor         # NEW — honest "no model loaded" + load-on-demand (LoadAsync via gate)
│   └── Catalog/ConfirmDialog.razor # (existing; REUSED for clear/delete consent)
├── MauiProgram.cs                 # CHANGED — register IChatHistoryStore (FileChatHistoryStore w/ AppData path) + app tools collection
└── wwwroot/                       # CHANGED — code-copy JS + markdown/code-block styles (Copper accent, AA, both themes)

tests/FoundryStudio.Tests/         # Core-only, dylib-free
├── TranscriptAssemblerTests.cs        # NEW — system + multi-turn ordering (SC-001)
├── InferenceParametersMappingTests.cs # NEW — 4 params flow into ChatOptions; zero unsupported keys (SC-006)
├── TokenStatsAccumulatorTests.cs      # NEW — TTFT/tok-per-sec/total/stop-reason + unknown over synthetic updates (SC-005)
├── ContextWindowEstimatorTests.cs     # NEW — advance/warn/unknown-context (SC-007)
├── ChatMarkdownTests.cs               # NEW — headings/lists/emphasis/inline + fenced-block extraction + HTML-injection sanitize (SC-002)
├── ChatHistoryDocumentTests.cs        # NEW — round-trip (turns + system prompt + params) survives reload (SC-008)
├── ChatHistoryConsentGateTests.cs     # NEW — delete(userConfirmed:false) removes nothing (SC-009, dylib-free)
└── ToolInvocationWiringTests.cs       # NEW — fake IChatClient through UseFunctionInvocation over a scripted stream invokes a real tool (SC-010)
```

**Structure Decision**: Single MAUI Blazor Hybrid desktop solution; extend the four existing projects (`FoundryStudio.App`, `.Core`, `.Foundry`, `.Tests`). All pure chat logic lives in `.Core` (FL-free, dylib-free, unit-tested) — including the new `IChatHistoryStore` + `FileChatHistoryStore` (plain managed file IO, directly testable with a temp dir), a deliberate, documented refinement over `FileSettingsService`'s Foundry-layer placement (research R3). FL access (pipeline build, options/usage/tools mapping, the example tools) stays in `.Foundry`; the chat UI in `.App` consumes only Core abstractions + `IChatService` + the M1–M3 services — preserving the Constitution V / DEC-004 layering. No new projects (Complexity Tracking not triggered).

## Complexity Tracking

> No Constitution Check violations. No entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | — | — |
