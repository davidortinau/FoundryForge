# Quickstart: M4 — Chat experience (validation & DevFlow e2e)

This is the **validation/run guide** that proves M4 works end-to-end. It is not implementation (that belongs in `tasks.md`). Two layers: (A) **dylib-free unit tests** for every Core seam, the history round-trip, the consent gate, the params mapping, and tool-invocation wiring (no hardware); and (B) a **real Apple-Silicon DevFlow DOM e2e** (Constitution II) whose only live destructive action targets a conversation the test itself created — pre-existing user history is never touched (FR-040).

## Prerequisites
- macOS / Apple Silicon; .NET 11 SDK (`global.json` pin); FL native chain present (M0 gate green).
- A model **loaded** via M3 (e.g. `qwen2.5-0.5b`); a tool-capable model for US8.
- DevFlow Agent/Blazor `0.25.0-dev` (KI-002). Evidence path = **DOM inspection** (KI-001): `maui devflow webview source` / `Runtime evaluate`. `ui screenshot` only as a frontmost-window supplement, never the gate.
- Markdig `0.44.0` referenced by `FoundryStudio.Core` (already pinned) — run `dotnet restore` to refresh `FoundryStudio.Core/packages.lock.json` (research R1).

## Layer A — dylib-free unit tests (no hardware, CI seam gate)
```bash
dotnet test tests/FoundryStudio.Tests/FoundryStudio.Tests.csproj
```
Expected green, covering (each maps to a Core seam in `contracts/core-seams.md`):
- `TranscriptAssemblerTests` — system + multi-turn ordering & roles (SC-001).
- `InferenceParametersMappingTests` — the four params flow into `ChatOptions`; **zero** unsupported-param surface; tools attach (SC-006).
- `TokenStatsAccumulatorTests` — TTFT / tok-per-sec / total over synthetic updates; **no usage ⇒ `TotalTokens == null` (honest "unknown")**; `Complete(UserCancelled)` honored (SC-005).
- `ContextWindowEstimatorTests` — advance / warn-threshold / **null context ⇒ unknown, no fraction** (SC-007).
- `ChatMarkdownTests` — headings/lists/emphasis render; fenced block extracted with exact code + language; **raw HTML/`<script>` encoded, not active** (SC-002).
- `ChatHistoryDocumentTests` — `ChatSession` (turns + system prompt + params) round-trips; garbage JSON recoverable (SC-008).
- `ChatHistoryConsentGateTests` — `DeleteAsync`/`ClearMessagesAsync(userConfirmed:false)` **remove nothing**; `true` removes. Proves the consent gate without any live FL/dylib (SC-009).
- `ToolInvocationWiringTests` — a fake `IChatClient` through `AsBuilder().UseFunctionInvocation().Build()` invokes a **real** test `AIFunction` and feeds its result back (SC-010).
- The `NoBlockingInitGuard` test stays green — no `.Result`/`.Wait()` in the chat path (KI-005, FR-037).
- Existing suites stay green.

## Layer B — DevFlow DOM e2e (real Apple Silicon)
Launch to ready, click `nav-chat` → `/chat`. Reference: `contracts/chat-ui.dom.md` (hooks), `contracts/service-surface.md` (wiring), `data-model.md` (state).

1. **Nav activation (US1 / FR-001)** — assert `nav-chat` is an enabled `NavLink` (no `disabled`/"Coming soon"); `/chat` shows `chat-page`.
2. **Gate + load-on-demand (US2 / SC-003)** — with **no** model loaded, assert `chat-gate` is shown and the composer does not accept a send; activate `chat-gate-load` → load runs via `LoadAsync` through the gate → surface becomes ready and `chat-active-model` names it. Force a busy path → honest `chat-busy` (`ModelBusyException`), no hang.
3. **Streaming multi-turn (US1 / SC-001)** — send a message; assert `chat-msg-assistant` renders **incrementally** (`data-streaming="true"` with multiple successive partial renders, then `false`) from real updates — never a single paste. Send a follow-up; assert prior turns are included as context.
4. **Markdown + code copy (US1 / SC-002)** — on a reply containing a fenced code block, assert `chat-codeblock` + `chat-code-copy`; activate copy and assert the exact code is on the clipboard; assert markdown (headings/lists/emphasis) renders and any raw HTML is inert.
5. **Awaiting-first-token (US1 / FR-006)** — before the first token, assert `chat-awaiting` ("pouring response") shows with **no** fabricated partial text; composer placeholder reads "Ask this local model…"; send uses Copper.
6. **Stop / cancel (US3 / SC-004)** — start a long generation; assert `chat-stop` is present **only** while streaming; activate it → streaming halts promptly, the partial text is preserved as a stopped turn (stop reason user-cancelled), surface returns to ready, next send works. Confirm no orphaned generation (lease released) and no `.Result`/`.Wait()` (KI-005).
7. **Honest metrics (US4 / SC-005)** — assert `chat-metrics` shows `metric-ttft`, `metric-tps`, `metric-total`, `metric-stop` derived from the real stream (TTFT matches first-update arrival); when usage is absent, `metric-total` shows **"unknown"** (0 fabricated values). Record observed FL usage/finish-reason behavior (research R2).
8. **System prompt + the four params (US5 / SC-006)** — set `chat-system-prompt`; assert it is applied as the system message. Adjust `param-temperature`/`param-max-tokens`/`param-top-p`/`param-frequency-penalty`; assert the values flow into the request (cross-check the unit-tested mapping). Assert `param-top-k`/`param-min-p`/`param-repeat-penalty`/`param-seed` are **absent** (FR-019).
9. **Context tracker (US6 / SC-007)** — grow the conversation; assert `chat-context` advances and is **labeled an estimate**; near the limit assert `chat-context-warn`; on a model with unreported context length assert `chat-context-unknown` with **no** fabricated denominator.
10. **Persisted history + consent (US7 / SC-008/009, Constitution IV)** —
    - Hold a conversation; **restart the app**; assert it reloads from `chat-conversations` with turns + system prompt + params intact (FR-023).
    - `chat-new` and `chat-duplicate` create persisted conversations non-destructively (source unchanged).
    - Activate `chat-delete` (or `chat-clear`) → assert a `confirm-dialog` opens that **names the test conversation** + states it **cannot be undone**, with `confirm-cancel` **default-focused**; assert **nothing** removed on this single activation.
    - Activate `confirm-cancel` → dialog closes, conversation intact.
    - Re-open, activate `confirm-accept` → conversation removed via `DeleteAsync(userConfirmed:true)`. **This live delete targets only the test-created conversation — never pre-existing user history (FR-040).**
11. **Tool calling (US8 / SC-010)** — on a tool-capable model, ask something warranting `get_current_time`/`get_loaded_model_info`; assert `chat-tool-activity` reflects a **real** invocation whose result is incorporated; a tool error surfaces as `chat-tool-error`, plainly. Assert **no** decorative tool affordance exists.
12. **Negative / honesty invariants (SC-005/006/007/011)** — assert **zero** unsupported-param controls, **zero** "guaranteed JSON"/enforcement toggle, **zero** fabricated tokens/metrics, and every unavailable value renders honest "unknown"/"estimate".
13. **Accessibility (SC-012)** — in **both** Workshop Daylight and Night Forge: every new control is labeled + keyboard-reachable; the streamed region is `aria-live`; the confirm dialog is focus-trapped with Cancel default-focused; states are text+icon (not color-only); contrast meets AA (computed-style query).

## Definition of Done (M4)
- [ ] Layer A unit tests green (CI seam gate clean; `NoBlockingInitGuard` green).
- [ ] Layer B DevFlow DOM e2e passes on real Apple Silicon (steps 1–13).
- [ ] Pre-existing user chat history untouched; the only live delete was the test-created conversation (FR-040).
- [ ] US9 (structured output / regenerate) optional — its absence does **not** block DoD (FR-033); if present, it is honestly labeled best-effort with no enforcement claim (FR-031).
- [ ] Independent reviewer (not the author) signs off (Constitution II/III).
- [ ] Milestone note ends with a **`Verified:`** line naming the checks that ran (unit suites + the DevFlow DOM e2e + observed FL usage/finish-reason, cancel-honoring, and tool round-trip behavior).

> `Verified:` (to be filled at close) — e.g. `Verified: dotnet test green (TranscriptAssembler/InferenceParametersMapping/TokenStatsAccumulator/ContextWindowEstimator/ChatMarkdown/ChatHistoryDocument/ChatHistoryConsentGate/ToolInvocationWiring + NoBlockingInitGuard + existing); DevFlow DOM e2e on M-series — nav-chat/gate+load-on-demand/streaming-multi-turn(partial renders real)/markdown+code-copy/stop(partial preserved)/metrics(TTFT real, total=<obs>)/4-params-only/context-estimate/persist-reload-after-restart/consent-delete-on-test-convo/tool-call(<obs>); user history untouched; FL usage=<obs>, finish-reason=<obs>, cancel-honored=<obs>, tools-honored=<obs>.`
