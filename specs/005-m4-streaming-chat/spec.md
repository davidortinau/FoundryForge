# Feature Specification: M4 — Chat experience (v1 core)

**Feature Branch**: `005-m4-streaming-chat`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "M4 — Chat experience (v1 core): a multi-turn streaming chat experience for talking to a locally-loaded Foundry Local model, entirely in-process (no loopback socket). The v1 lighthouse payoff after M0 (feasibility), M1 (shell/services), M2 (catalog browse), M3 (model install/management). Chat runs through the existing in-process `IChatService.StreamAsync` adapter over `FoundryChatClient` (Microsoft.Extensions.AI). Tool/function calling is in scope (M0d confirmed FL honors `tools`); structured output is best-effort only (accepted-but-not-enforced); only FL's real inference params (temperature, max tokens, top_p, frequency_penalty) are surfaced; chat gates on a loaded model with load-on-demand; conversations persist to app data; streaming generations are cancellable; honest token metrics and an estimated context-window tracker are shown."

## Overview

M3 is DONE: the catalog is now an actionable model manager — a user can download, load/unload (through the M1 `IModelStateGate`), delete (consent-gated), pin a variant, and configure the cache directory. The sidebar "Chat" nav still renders a disabled **"Coming soon"** placeholder. **M4 activates it** and ships the v1 lighthouse payoff: an actual, multi-turn, *streaming* conversation with a model the user has loaded on this Mac.

**M4 is in-process, not a server.** Chat runs entirely through the existing in-process MEAI adapter — `FoundryStudio.Core.Abstractions.IChatService.StreamAsync(IEnumerable<ChatMessage>, ChatOptions?, CancellationToken) → IAsyncEnumerable<ChatResponseUpdate>`, implemented by `FoundryStudio.Foundry.ChatService` over `FoundryChatClient`. There is **no loopback socket** in this path. The local OpenAI-compatible server is M5 and exists only to expose the model to *external* tools; it is never how FoundryStudio's own chat talks to the model. The `ChatService` seam already documents the M4 pipeline shape: `adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()`.

M4 builds on confirmed feasibility from **M0d**: the Foundry Local chat path **honors `tools`** (function calling works end-to-end), and `response_format` / structured output is **accepted-but-not-enforced** (best-effort only). This split is binding on scope: tool/function calling is **in** M4; structured output is **best-effort only**, with no "guaranteed JSON" claim and no toggle that implies enforcement.

The single most important constraint of M4 is **honesty (Constitution III) and data preservation (Constitution IV)**. Every token shown is a real streamed token from the live `IAsyncEnumerable<ChatResponseUpdate>`; every metric (time-to-first-token, tokens/sec, total tokens, stop reason) comes straight from the real stream and never from a fabricated or animated source. Only the four inference parameters Foundry Local's `ChatSettings` actually supports — **temperature, max tokens, top_p, frequency_penalty** — are surfaced; `top_k` / `min_p` / `repeat_penalty` / `seed` **do not exist** in Foundry Local and MUST NOT appear as controls (a fake control violates Constitution IV). The context-window tracker is an explicit *estimate* and never claims exactness. Chat history is user data: it persists across restart, and destructive actions (clear/delete a conversation) use the named consent pattern ("cannot be undone", Cancel default-focus) per Constitution IV. No `.Result` / `.Wait()` on async anywhere (KI-005).

Because this is a screen-bearing milestone, success is framed around **observable UI behavior verifiable via DevFlow DOM inspection** (per KI-001 — the WebView pixel screenshot needs the window frontmost, so DOM is the sanctioned autonomous evidence path) **and the pure-logic seams** (metrics derivation from a fake stream, context-window estimate, history persistence/serialization, the consent gate, params→`ChatOptions` mapping, tool-invocation wiring) that are unit-testable without a native dylib.

## Clarifications

No outstanding clarifications. All open choices were resolved using reasonable defaults derived from `docs/PLAN.md` (the M4 milestone lines 112–119 and the v1 lighthouse line 120; the Parity Map rows for inference params and structured output), `docs/DESIGN.md` (§8 metrics row + composer, §9 voice — "Ask this local model…", "pouring response" streaming voice, "guaranteed JSON" prohibition, Foundry Copper accent, Workshop Daylight + Night Forge themes, WCAG AA), `.specify/memory/constitution.md` (III Honesty; IV Data Preservation & Capability Honesty; V Native-Load & In-Process Discipline; II Pre-Completion Verification), the M0d feasibility finding (FL honors `tools`; `response_format` accepted-but-not-enforced), `KNOWN-ISSUES.md` (KI-001 DOM-evidence path; KI-005 no `.Result`/`.Wait()`), and the real code in `src/` (`IChatService`/`ChatService`/`FoundryChatClient`, the M1 `IModelStateGate`, the M3 load path). The resulting defaults are recorded in the **Assumptions** section.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Multi-turn streaming chat with a loaded local model (Priority: P1)

As a FoundryStudio user with a model loaded, I want to type a message and watch the model's reply stream in token-by-token, then keep the conversation going across multiple turns — with markdown rendered and code blocks given a copy button — so that I can actually *use* a local model in a real back-and-forth conversation on my Mac.

**Why this priority**: This is the headline payoff of the entire v1 lighthouse — "I can chat with a local model, fully offline, in-process." It is the smallest slice that delivers the milestone's standalone end-user value, and every other story refines it. The seam (`IChatService.StreamAsync` → `IAsyncEnumerable<ChatResponseUpdate>`) already exists; M4 builds the chat surface that consumes it.

**Independent Test**: On an Apple Silicon Mac with a model loaded (via M3), open the Chat surface, send a message, and confirm via DevFlow DOM that assistant text appears incrementally (multiple successive partial renders, not a single final paste) sourced from the real streamed updates; send a second message and confirm prior turns are retained as context (multi-turn). Confirm a fenced code block in the reply renders as a code block with a working Copy control, and that markdown (headings, lists, emphasis) renders. The transcript-assembly and markdown/code-block detection logic are independently unit-testable as pure seams over a synthetic `ChatResponseUpdate` sequence with no native dylib.

**Acceptance Scenarios**:

1. **Given** a loaded model and the Chat surface open, **When** the user sends a message, **Then** the assistant reply renders incrementally as real tokens arrive from the `IAsyncEnumerable<ChatResponseUpdate>` stream (multiple partial updates), never as a single post-hoc paste and never from a fabricated/typewriter-only animation.
2. **Given** a completed assistant turn, **When** the user sends a follow-up, **Then** the full prior conversation (system + user + assistant turns) is sent as context so the exchange is genuinely multi-turn.
3. **Given** an assistant reply containing markdown, **When** it renders, **Then** headings, lists, emphasis, inline code, and fenced code blocks display correctly.
4. **Given** a fenced code block in a reply, **When** it renders, **Then** it is shown as a distinct code block with a Copy control that copies the exact code to the clipboard.
5. **Given** an in-flight stream, **When** the model has not yet produced its first token, **Then** the UI shows an honest "pouring response" / awaiting-first-token state (no fabricated partial text).
6. **Given** the composer placeholder, **When** the surface is empty/idle, **Then** it reads "Ask this local model…" and the send affordance uses the Foundry Copper accent (per DESIGN §8/§9).
7. **Given** the chat surface and controls, **When** reached via assistive technology in both Workshop Daylight and Night Forge themes, **Then** the composer, send, streamed message region, and Copy controls are labeled, keyboard-reachable, and meet WCAG AA (no information by color alone).

---

### User Story 2 - Gate chat on a loaded model, with load-on-demand (Priority: P1)

As a FoundryStudio user, when I open Chat without a model loaded I want to be told clearly that a model must be loaded first and be offered a one-step way to load one (reusing the existing load path), rather than typing into a dead box or getting a cryptic failure — so that I always know whether chat is ready and can make it ready.

**Why this priority**: Chat is meaningless without a loaded model, and silently failing or pretending to chat would violate honesty. Gating + load-on-demand is the prerequisite that makes US1 reliably usable. Loading already exists (M3 `LoadAsync` through the `IModelStateGate`); M4 surfaces the gate and a load-on-demand entry from the chat surface.

**Independent Test**: With no model loaded, open Chat and confirm via DevFlow DOM that an honest "no model loaded" gate is shown with an offer to load one, and that the composer does not pretend to be ready. Trigger load-on-demand for a cached model and confirm the surface transitions to chat-ready once the load completes through the gate. Force a concurrent load contention and confirm the gate's rejection (`ModelBusyException`) surfaces as an honest "model busy" message, not a hang. The ready/not-ready gating logic and the loaded-model resolution are independently unit-testable as pure seams.

**Acceptance Scenarios**:

1. **Given** no model is loaded, **When** the user opens Chat, **Then** an honest gate explains a model must be loaded and offers to load one — the composer does not accept a send as if it would work.
2. **Given** the load-on-demand offer, **When** the user loads a cached model, **Then** the load runs through the existing path (`LoadAsync` via the `IModelStateGate`) and the surface becomes chat-ready, identifying the loaded model.
3. **Given** a model is already loaded when Chat opens, **When** the surface renders, **Then** it is immediately chat-ready and names the active model — no redundant prompt.
4. **Given** a load is rejected by the gate because another mutation is in flight (`ModelBusyException`), **When** the rejection occurs, **Then** the UI surfaces an honest "model is busy, try again" state and does not hang or corrupt the surface.
5. **Given** a model is unloaded elsewhere (e.g., from the catalog) while Chat is open, **When** the loaded state changes, **Then** the chat surface honestly reflects that it is no longer ready rather than continuing to accept sends silently.

---

### User Story 3 - Cancel / stop a streaming generation mid-flight (Priority: P1)

As a FoundryStudio user, while the model is streaming a reply I want a clear Stop control that halts generation immediately and keeps whatever was produced so far — so that I am never trapped waiting on a long or off-track answer.

**Why this priority**: Streaming without a stop is a trap; cancellation is a core, expected chat affordance and a correctness requirement (clean cancellation, no orphaned async, no `.Result`/`.Wait()` per KI-005). It is P1 because US1 is not genuinely usable without it.

**Independent Test**: Start a generation likely to be long, activate Stop, and confirm via DevFlow DOM that streaming halts promptly, the partial reply so far is preserved as a completed (stopped) turn, the surface returns to ready, and a subsequent send works normally. The cancellation wiring (a `CancellationToken` threaded into `StreamAsync`, cooperative stop, no thread-blocking await) is independently unit-testable as a pure seam over a controllable fake stream.

**Acceptance Scenarios**:

1. **Given** an in-flight streaming reply, **When** the user activates Stop, **Then** generation is cancelled via the `CancellationToken` passed to `StreamAsync`, streaming halts promptly, and the partial text already produced is preserved (not discarded, not presented as complete).
2. **Given** a cancelled generation, **When** it stops, **Then** the surface returns to ready and the next send proceeds normally with the cancelled turn retained in history.
3. **Given** cancellation, **When** the stream unwinds, **Then** no `.Result`/`.Wait()` is used and no orphaned/background generation continues after Stop (KI-005; clean cooperative cancellation).
4. **Given** a generation that completes on its own, **When** it finishes, **Then** the Stop control is replaced by the normal send/ready affordance (Stop is only present while streaming).

---

### User Story 4 - Honest token metrics from the real stream (Priority: P1)

As a FoundryStudio user, after (and during) a reply I want to see real performance metrics — time-to-first-token, tokens/sec, total tokens, and the stop reason — shown in the calm mono metrics row, sourced straight from the engine, so that I can judge this model's real local performance and never see a fabricated number.

**Why this priority**: Honest metrics are a defining promise of FoundryStudio (Constitution III) and a primary reason a developer evaluates a *local* model. The DESIGN already specifies the metrics row (mono, tertiary: tokens/sec · token count · time · stop reason). It is P1 because shipping chat without honest metrics — or with faked ones — would break the product's core trust contract.

**Independent Test**: Send a message and confirm via DevFlow DOM that the metrics row shows TTFT, tokens/sec, total tokens, and a stop reason, that the values are plausible and derived from the real stream timing/usage (e.g., TTFT matches first-update arrival; total tokens matches the stream's usage when available), and that when a metric is genuinely unavailable from the engine it renders an honest "unknown" rather than a guessed value. The metrics-derivation logic (TTFT from first-update timestamp, tokens/sec from token count over elapsed, total from usage, stop reason mapping, the unknown case) is fully unit-testable as a pure seam over a synthetic `ChatResponseUpdate` sequence.

**Acceptance Scenarios**:

1. **Given** a streaming reply, **When** the first token arrives, **Then** time-to-first-token is measured from send to that first real update — not from a placeholder or animation.
2. **Given** a reply in progress / completed, **When** the metrics row renders, **Then** tokens/sec, total tokens, and time are derived from the real stream (token counts and timing actually observed), shown in mono tertiary text per DESIGN §8.
3. **Given** a completed reply, **When** the stop reason is known from the stream (e.g., natural stop, max-tokens reached, user-cancelled), **Then** the real stop reason is displayed; a user Stop (US3) is reported as user-cancelled.
4. **Given** a metric the engine does not provide for a given response (e.g., usage/total tokens absent), **When** the row renders, **Then** that metric shows an honest "unknown" — never a fabricated or back-computed-and-presented-as-exact number.
5. **Given** the metrics row, **When** reached via assistive technology, **Then** each metric is labeled and its meaning is perceivable (WCAG AA), in both themes.

---

### User Story 5 - Per-chat system prompt and the real inference parameters (Priority: P2)

As a FoundryStudio user, I want to set a system prompt for a conversation and adjust the inference parameters that Foundry Local actually supports — temperature, max tokens, top_p, frequency_penalty — so that I can steer the model's behavior, while trusting that every control shown is real.

**Why this priority**: System prompt and parameter control are how a developer meaningfully exercises a model, but they refine the P1 chat rather than enabling it (chat works at defaults). Constitution IV makes the *honesty* of the parameter surface non-negotiable: only the four FL-supported params may appear, and absent ones must not be faked.

**Independent Test**: Set a per-chat system prompt and confirm via DevFlow DOM that it is applied to the conversation (sent as the system message). Adjust each of the four parameters and confirm via the params→`ChatOptions` mapping (a pure unit-testable seam) that the value flows into the request. Confirm via DOM that **no** control exists for `top_k`, `min_p`, `repeat_penalty`, or `seed`. The mapping of the four supported params into `ChatOptions`, and the absence of unsupported params, are independently unit-testable without a native dylib.

**Acceptance Scenarios**:

1. **Given** a conversation, **When** the user sets a system prompt, **Then** it is applied as the system message for that conversation's requests and persists with the conversation.
2. **Given** the parameter controls, **When** the user adjusts temperature, max tokens, top_p, or frequency_penalty, **Then** the value is carried into the request (`ChatOptions`/FL `ChatSettings`) and takes effect on the next generation.
3. **Given** the parameter surface, **When** it renders, **Then** it exposes **only** temperature, max tokens, top_p, and frequency_penalty — and shows **no** control for `top_k`, `min_p`, `repeat_penalty`, or `seed` (these do not exist in Foundry Local and MUST NOT appear, per Constitution IV).
4. **Given** each parameter control, **When** shown, **Then** its honest range/meaning is conveyed and defaults are sensible; no control implies a capability the engine lacks.
5. **Given** the system-prompt and parameter controls, **When** reached via assistive technology, **Then** they are labeled, keyboard-reachable, and WCAG AA in both themes.

---

### User Story 6 - Context-window tracker with an honest estimate warning (Priority: P2)

As a FoundryStudio user, I want to see roughly how much of the model's context window the current conversation is using and get a warning as I approach the limit — clearly labeled as an *estimate* — so that I understand when older turns may fall out of context, without being told a false exact figure.

**Why this priority**: Long conversations silently exceeding context produce confusing model behavior; a tracker helps the user manage it. It refines the P1 chat experience and is P2 because chat functions without it. Honesty is the binding constraint: token counting against a model's context length is an approximation and MUST be presented as such (Constitution III — never claim exactness we cannot prove).

**Independent Test**: Build up a conversation and confirm via DevFlow DOM that a context-usage indicator advances and that a warning appears as usage approaches the model's reported context length, with copy that clearly labels the figure an estimate. Confirm that when the model's context length is unknown, the tracker honestly says so rather than inventing a denominator. The estimate logic (approximate token count, fraction-of-context, warn threshold, the unknown-context case) is fully unit-testable as a pure seam.

**Acceptance Scenarios**:

1. **Given** an ongoing conversation, **When** it grows, **Then** a context-usage indicator reflects an estimated token usage against the model's context length and is explicitly labeled an estimate (not an exact count).
2. **Given** usage approaching the context length, **When** a warn threshold is crossed, **Then** an honest warning appears explaining older turns may fall out of context — without claiming precise numbers.
3. **Given** a model whose context length is not reported, **When** the tracker renders, **Then** it honestly indicates the limit is unknown and does not fabricate a denominator or a false percentage.
4. **Given** the tracker, **When** reached via assistive technology, **Then** its value and warning state are labeled and perceivable (WCAG AA), with the estimate qualifier conveyed (not by color alone).

---

### User Story 7 - Persisted chat history with consent-gated destructive actions (Priority: P2)

As a FoundryStudio user, I want my conversations saved automatically and available after I restart the app, with the ability to start a new chat, duplicate one, and clear/delete one — but only delete behind an explicit confirmation that names the conversation and says it cannot be undone — so that my chat history (my data) is preserved and never destroyed by accident.

**Why this priority**: Chat history is user data under Constitution IV; persistence + a correct consent flow are required for the milestone to be trustworthy, but they layer on the P1 conversation rather than enabling it. New/duplicate are non-destructive conveniences; clear/delete are the safety-critical part.

**Independent Test**: Hold a conversation, restart the app, and confirm via DevFlow DOM that the conversation (turns, system prompt, params) is restored. Create a new chat and duplicate an existing one and confirm both via DOM. Activate Clear/Delete and confirm a confirmation names the exact conversation and states it cannot be undone, with Cancel as the default-focused action; choosing Cancel deletes nothing; confirming removes it. The persistence/serialization round-trip and the consent-gate logic are independently unit-testable as pure seams without a native dylib (and any live destructive delete in verification targets only a conversation the test itself created).

**Acceptance Scenarios**:

1. **Given** one or more conversations, **When** the app is restarted, **Then** they are reloaded from app data with their turns, system prompt, and parameters intact (survive restart).
2. **Given** the chat surface, **When** the user starts a new chat or duplicates an existing one, **Then** the new conversation is created and persisted without altering the source (non-destructive).
3. **Given** a conversation, **When** the user activates Clear/Delete, **Then** an explicit confirmation is presented that names the exact conversation and states the action **cannot be undone**, with **Cancel** as the default-focused control — deletion does NOT proceed on the single activation.
4. **Given** the delete/clear confirmation, **When** the user cancels, **Then** nothing is deleted and the conversation remains intact.
5. **Given** the delete/clear confirmation, **When** the user confirms, **Then** the conversation (or its messages, for clear) is removed from app data and the UI updates honestly.
6. **Given** a conversation is updated (new turn, params change), **When** the change occurs, **Then** it is persisted such that it would survive a restart — no silent loss of an exchange the user saw.

---

### User Story 8 - Tool / function calling with minimal, real .NET tools (Priority: P2)

As a FoundryStudio user, I want the model to be able to call one or two genuine app-provided tools (functions) during a conversation when relevant — wired via MEAI `UseFunctionInvocation()` — so that I can see real local function calling working, not a tool UI that does nothing.

**Why this priority**: M0d confirmed Foundry Local honors `tools`, so function calling is a real, demonstrable capability and a meaningful part of the lighthouse. It is P2 because plain chat (P1) stands without it, and the v1 tool surface is deliberately minimal. Constitution IV forbids shipping dead UI: any tool surfaced must actually work.

**Independent Test**: With a tool-capable loaded model, ask something that should trigger an app-defined tool (e.g., a genuine example tool), and confirm via DevFlow DOM that the tool is invoked and its real result is incorporated into the reply — and confirm via the pipeline wiring (`adapter.AsBuilder().UseFunctionInvocation()...Build()`) that invocation is handled by MEAI middleware, not faked. The tool-registration and invocation-handling wiring is independently unit-testable as a pure seam (a fake tool invoked through `UseFunctionInvocation` over a scripted stream) without a native dylib.

**Acceptance Scenarios**:

1. **Given** a loaded model and one or two app-defined .NET tools registered, **When** a user message warrants a tool, **Then** the model's tool call is executed via `UseFunctionInvocation()` and its real result is fed back into the response (genuine function calling, end-to-end).
2. **Given** the v1 tool surface, **When** it ships, **Then** it includes only genuine, working tools (e.g., one or two real example tools) — there is NO tool affordance that is decorative or does nothing (Constitution IV: no dead UI).
3. **Given** a tool invocation, **When** it runs, **Then** any tool activity surfaced to the user is honest (it reflects a tool that actually executed), and a tool error is surfaced plainly rather than hidden or faked.
4. **Given** a model/turn where no tool is needed, **When** the user chats normally, **Then** tools do not interfere and plain streaming chat behaves exactly as in US1.

---

### User Story 9 - [OPTIONAL] Best-effort structured output and regenerate-last (Priority: P3)

As a FoundryStudio user, I may want to request structured (JSON) output and to regenerate the last reply — understanding that structured output is **best-effort only** (Foundry Local accepts but does not enforce `response_format`) and is never promised as guaranteed.

**Why this priority**: Both are nice refinements, not lighthouse-critical. M0d found structured output **accepted-but-not-enforced**, so it may be passed through but MUST NOT be promised; a dedicated "guaranteed JSON" toggle is forbidden (Constitution III/IV — no fake enforcement). Regenerate-last is an explicitly-optional simple convenience (full branching/regenerate trees are out of scope). This story is lowest priority and may be deferred without affecting M4's definition of done.

**Independent Test**: (Structured output) If implemented, confirm via DevFlow DOM and copy review that any structured-output affordance is clearly labeled **best-effort** (it does not claim guaranteed/enforced JSON) and that `response_format` is merely passed through; confirm there is **no** toggle implying enforcement — preferably the feature is omitted rather than mislabeled. (Regenerate) If implemented, confirm via DOM that regenerating the last reply re-streams a fresh reply for the same prior context and replaces the last assistant turn. Both behaviors (pass-through without an enforcement claim; regenerate-last replacing the last turn) are unit-testable as pure seams.

**Acceptance Scenarios**:

1. **Given** structured output, **When** it is surfaced at all, **Then** it is labeled **best-effort only** and `response_format` is passed through without any claim or UI implying the output is guaranteed/enforced JSON (Constitution III — never say "guaranteed JSON").
2. **Given** the honesty constraint, **When** a "guaranteed JSON" or enforcement-implying toggle would be the only honest-feeling option, **Then** the feature is **omitted** rather than shipped misleadingly.
3. **Given** a completed assistant turn, **When** the user (optionally) regenerates the last reply, **Then** a fresh reply is streamed for the same prior context and replaces the previous last assistant turn (simple regenerate-last only — no branching tree).
4. **Given** this story is deferred, **When** M4 ships without it, **Then** M4's definition of done is unaffected (it is optional/P3).

---

### Edge Cases

- **No model loaded on open** — Chat shows the honest gate + load-on-demand (US2), never a dead composer that pretends to send.
- **Model unloaded mid-session** — surface reflects not-ready rather than silently dropping a send (US2.5).
- **First token never arrives / very slow model** — the "pouring response" awaiting state holds honestly; Stop remains available (US1.5/US3).
- **Stream errors mid-generation** (engine fault, OOM) — surface an honest error naming the diagnosed cause; preserve any partial text; return to ready (no fake completion).
- **User Stop during the very first chunk** — partial (possibly empty) turn preserved; stop reason = user-cancelled (US3/US4.3).
- **Usage/total-token data absent from the stream** — metrics show honest "unknown", not a fabricated total (US4.4).
- **Context length unknown for the model** — tracker says limit unknown; no invented denominator (US6.3).
- **Conversation exceeds context** — warning fires; behavior is the engine's real truncation, surfaced honestly as an estimate (US6).
- **App killed mid-stream** — on restart, the last persisted state loads; an interrupted turn is not presented as a clean completion (US7.6).
- **Tool throws / returns an error** — surfaced plainly, not hidden; chat does not crash (US8.3).
- **Empty or whitespace-only send** — rejected gracefully (no empty turn, no wasted generation).
- **Very large code block / very long reply** — renders and stays copyable and scrollable; Copy copies exact content (US1.4).
- **Concurrent send while a stream is in flight** — only one active generation per conversation; the surface prevents or queues honestly (no two interleaved streams in one turn).
- **Clear vs Delete on the active conversation** — both go through the named consent flow; the active surface updates honestly afterward (US7).

## Requirements *(mandatory)*

### Functional Requirements

**Streaming chat core (US1)**

- **FR-001**: The app MUST activate the sidebar "Chat" nav (replacing the disabled "Coming soon" placeholder) and present a chat surface for conversing with the loaded model.
- **FR-002**: Chat MUST run entirely in-process through `IChatService.StreamAsync(IEnumerable<ChatMessage>, ChatOptions?, CancellationToken)` over `FoundryChatClient` (MEAI) — it MUST NOT use a loopback socket or the M5 local server for its own conversation.
- **FR-003**: Assistant replies MUST render incrementally from the real `IAsyncEnumerable<ChatResponseUpdate>` stream (multiple partial updates), never as a single post-hoc paste and never from a fabricated/typewriter-only animation.
- **FR-004**: Conversations MUST be multi-turn: each send MUST include the prior conversation (system + user + assistant turns) as context.
- **FR-005**: Assistant content MUST render markdown (headings, lists, emphasis, inline code) and MUST render fenced code blocks as distinct blocks each with a Copy control that copies the exact code.
- **FR-006**: While awaiting the first token, the UI MUST show an honest awaiting/"pouring response" state with no fabricated partial text; the composer placeholder MUST read "Ask this local model…" and the send affordance MUST use the Foundry Copper accent (DESIGN §8/§9).

**Loaded-model gate + load-on-demand (US2)**

- **FR-007**: When no model is loaded, the chat surface MUST present an honest gate explaining a model must be loaded and MUST offer to load one; it MUST NOT accept a send as though it would succeed.
- **FR-008**: Load-on-demand MUST reuse the existing load path (`LoadAsync` via the M1 `IModelStateGate`); on success the surface MUST become chat-ready and identify the active model.
- **FR-009**: When a model is already loaded, the surface MUST be immediately chat-ready and name the active model; when a model is unloaded elsewhere while Chat is open, the surface MUST honestly reflect not-ready.
- **FR-010**: When a load is rejected by the gate (`ModelBusyException` / in-flight mutation), the UI MUST surface an honest "model busy, try again" state and MUST NOT hang or corrupt the surface.

**Cancel / stop (US3)**

- **FR-011**: While streaming, the UI MUST present a Stop control that cancels generation via the `CancellationToken` passed to `StreamAsync`; on Stop, streaming MUST halt promptly and any partial text MUST be preserved (not discarded, not presented as complete).
- **FR-012**: After cancellation, the surface MUST return to ready, retain the stopped turn in history, and allow the next send normally.
- **FR-013**: Cancellation MUST be clean and cooperative with no orphaned/background generation continuing after Stop and no `.Result`/`.Wait()` on async (KI-005); the Stop control MUST be present only while streaming.

**Honest token metrics (US4)**

- **FR-014**: The chat surface MUST show a metrics row (mono, tertiary text per DESIGN §8) with time-to-first-token, tokens/sec, total tokens, and stop reason — each derived from the real stream (timing/usage actually observed), never fabricated.
- **FR-015**: Time-to-first-token MUST be measured from send to the first real streamed update; stop reason MUST reflect the real terminating condition (natural stop, max-tokens, user-cancelled, error).
- **FR-016**: When a metric is genuinely unavailable from the engine for a response (e.g., usage/total tokens absent), the row MUST show an honest "unknown" — never a fabricated or guessed-as-exact value.

**System prompt + real inference params (US5)**

- **FR-017**: Users MUST be able to set a per-conversation system prompt that is applied as the system message and persists with the conversation.
- **FR-018**: Users MUST be able to adjust the inference parameters Foundry Local actually supports — **temperature, max tokens, top_p, frequency_penalty** — and these MUST flow into the request (`ChatOptions` → FL `ChatSettings`) and take effect on the next generation.
- **FR-019**: The parameter surface MUST expose **only** those four parameters and MUST NOT surface any control for `top_k`, `min_p`, `repeat_penalty`, or `seed` (these do not exist in Foundry Local; surfacing them would be a fake control, violating Constitution IV).

**Context-window tracker (US6)**

- **FR-020**: The surface MUST show a context-usage indicator reflecting an estimated token usage against the model's context length, explicitly labeled an **estimate** (never an exact count).
- **FR-021**: As estimated usage approaches the context length, the UI MUST show an honest warning that older turns may fall out of context, without claiming precise figures.
- **FR-022**: When the model's context length is unknown, the tracker MUST honestly indicate the limit is unknown and MUST NOT fabricate a denominator or percentage.

**Persisted history + consent (US7)**

- **FR-023**: Conversations (turns, system prompt, parameters) MUST persist to app data and reload on app start so they survive restart.
- **FR-024**: Users MUST be able to start a new chat and duplicate an existing one; both MUST be non-destructive (the source is unchanged) and persisted.
- **FR-025**: Clearing or deleting a conversation MUST require an explicit confirmation that names the exact conversation and states the action **cannot be undone**, with **Cancel** as the default-focused control; a single activation MUST NOT delete.
- **FR-026**: The confirmation's Cancel path MUST delete nothing; only on explicit confirm may the conversation (or, for clear, its messages) be removed from app data, with the UI updating honestly.
- **FR-027**: Conversation updates (new turn, params change) MUST be persisted so an exchange the user saw is not silently lost across restart; an interrupted/in-flight turn MUST NOT be persisted as a clean completion.

**Tool / function calling (US8)**

- **FR-028**: The chat pipeline MUST support tool/function calling via MEAI `UseFunctionInvocation()` (e.g., `adapter.AsBuilder().UseFunctionInvocation()...Build()`), registering one or two genuine app-defined .NET tools.
- **FR-029**: A model tool call MUST be executed through `UseFunctionInvocation()` and its real result fed back into the response (genuine end-to-end function calling); tool errors MUST be surfaced plainly, not hidden or faked.
- **FR-030**: The v1 tool surface MUST contain only genuine, working tools — NO decorative or do-nothing tool affordance may ship (Constitution IV: no dead UI). When no tool is needed, plain streaming chat MUST behave exactly as US1.

**Structured output + regenerate (US9, optional/P3)**

- **FR-031** *(optional)*: If structured output is surfaced at all, it MUST be labeled **best-effort only** and merely pass `response_format` through; it MUST NOT claim or imply guaranteed/enforced JSON, and MUST NOT ship an enforcement-implying toggle — prefer omission over a misleading control (Constitution III/IV).
- **FR-032** *(optional)*: If implemented, regenerate-last MUST re-stream a fresh reply for the same prior context and replace the previous last assistant turn (simple regenerate-last only — no branching tree).
- **FR-033**: M4's definition of done MUST NOT depend on US9 (it is optional/P3 and may be deferred).

**Cross-cutting (honesty, layering, accessibility, verification)**

- **FR-034**: The chat UI MUST consume only `IChatService` (and Core pure seams / existing M1–M3 services); it MUST NOT call the Foundry Local SDK directly (Constitution V / layering).
- **FR-035**: M4 MUST NOT show any fabricated token, metric, progress, or capability; every streamed token and every metric MUST come from the real stream, and genuinely-unavailable values MUST render as honest "unknown"/"estimate"/"unsupported" (Constitution III).
- **FR-036**: All new interactive controls (composer/send, Stop, system-prompt + param controls, context tracker, history new/duplicate/clear/delete + confirmation, code-block Copy, any tool/structured affordances) MUST meet WCAG AA — labeled, keyboard-reachable, state changes perceivable, no information by color alone — in both Workshop Daylight and Night Forge themes.
- **FR-037**: No `.Result` / `.Wait()` (or other sync-over-async blocking) may be used anywhere in the chat path (KI-005); streaming and cancellation MUST be fully async.
- **FR-038**: The pure-logic seams introduced by M4 (transcript assembly, markdown/code-block detection, metrics derivation, context-window estimate, params→`ChatOptions` mapping, history persistence/serialization, consent-gate behavior, tool-invocation wiring) MUST be unit-testable without a native dylib.
- **FR-039**: M4 MUST close with a real Apple-Silicon DevFlow end-to-end check (load → multi-turn streaming chat → Stop → metrics observed → persisted conversation reloaded after restart, observed via DOM per KI-001) and a `Verified:` line; the reviewer MUST be independent of the author (Constitution II/III).
- **FR-040**: Verification MUST NOT destroy a user's pre-existing chat history: any live destructive clear/delete is performed only on a conversation the test itself created; the consent flow is proven without touching pre-existing conversations.

### Key Entities *(include if feature involves data)*

- **Conversation**: A persisted multi-turn chat. Attributes: id, title/name, ordered messages, per-chat system prompt, inference parameters (temperature, max tokens, top_p, frequency_penalty), the model alias it was last used with, created/updated timestamps. Survives restart; is user data under Constitution IV.
- **ChatMessage** (MEAI `ChatMessage`): A single turn with a role (system/user/assistant/tool) and content. The system message carries the per-chat system prompt. Assembled into the request sent to `StreamAsync`.
- **ChatResponseUpdate** (MEAI): A streamed increment from the engine — the only honest source of assistant text, timing, usage, and stop reason. M4 never fabricates these.
- **InferenceParameters**: The four real FL-supported values (temperature, max tokens, top_p, frequency_penalty) mapped into `ChatOptions` / FL `ChatSettings`. Explicitly excludes `top_k`/`min_p`/`repeat_penalty`/`seed`.
- **GenerationMetrics**: Derived-from-stream values — TTFT, tokens/sec, total tokens, stop reason — each either real or honest "unknown". Display-only; never persisted as if authoritative when unknown.
- **ContextWindowEstimate**: Estimated token usage vs the model's reported context length, plus a warn threshold and an unknown-context state. Explicitly an estimate.
- **Tool** (app-defined .NET function, MEAI `AIFunction`): A genuine, working function registered into the `UseFunctionInvocation()` pipeline. v1 ships only one or two real tools.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with a loaded model can hold a multi-turn conversation where assistant replies appear incrementally from the real stream (multiple partial renders observed via DOM) in 100% of sends on a working model — never a single post-hoc paste and never a fabricated animation.
- **SC-002**: Markdown renders and every fenced code block exposes a Copy control that copies the exact code, verified on Apple Silicon via DOM, in 100% of replies containing code.
- **SC-003**: Opening Chat with no model loaded shows the honest gate + load-on-demand in 100% of cases (never a dead composer), and load-on-demand makes the surface chat-ready via the existing `IModelStateGate` load path.
- **SC-004**: A user can Stop an in-flight generation; streaming halts promptly, partial text is preserved, the surface returns to ready, and no orphaned generation or `.Result`/`.Wait()` is used — in 100% of Stop activations.
- **SC-005**: The metrics row shows TTFT, tokens/sec, total tokens, and stop reason derived from the real stream; a genuinely-unavailable metric renders "unknown" — with 0 fabricated metric values ever displayed.
- **SC-006**: The parameter surface exposes exactly the four FL-supported params and 0 controls for `top_k`/`min_p`/`repeat_penalty`/`seed`; each of the four flows into the request, proven by a dylib-free unit test of the params→`ChatOptions` mapping.
- **SC-007**: The context-window tracker advances and warns near the limit, is labeled an estimate in 100% of states, and shows "unknown" (0 fabricated denominators) when the model's context length is unreported.
- **SC-008**: A conversation (turns, system prompt, params) survives an app restart and reloads from app data in 100% of cases; new and duplicate are non-destructive to the source.
- **SC-009**: Clearing/deleting a conversation is impossible without an explicit confirmation that names it and states it cannot be undone with Cancel default-focused; 0 deletions occur on a single activation, and Cancel deletes nothing in 100% of cancellations — proven by a dylib-free unit test of the consent gate.
- **SC-010**: Tool/function calling works end-to-end through `UseFunctionInvocation()` for at least one genuine app-defined tool (real result incorporated into a reply), verified on Apple Silicon; 0 decorative/do-nothing tool affordances ship.
- **SC-011**: Structured output, if surfaced at all, is labeled best-effort with 0 "guaranteed/enforced JSON" claims or enforcement-implying toggles (or it is omitted); if regenerate-last ships, it replaces the last assistant turn with a fresh stream for the same context.
- **SC-012**: All new chat controls meet WCAG AA (labeled, keyboard-reachable, state perceivable, not color-only) in both Workshop Daylight and Night Forge themes.
- **SC-013**: M4 closes with a real Apple-Silicon DevFlow end-to-end run (load → multi-turn streaming chat → Stop → honest metrics → conversation reloaded after restart, observed via DOM) and a `Verified:` line, reviewed by an independent reviewer, with the user's pre-existing chat history untouched.

## Assumptions

- **In-process only**: M4 chat uses `IChatService.StreamAsync` over `FoundryChatClient` with no loopback socket; the M5 local server is out of scope and is for external tools only (PLAN M4 line 113; constraint confirmed).
- **M0d findings are binding**: FL honors `tools` (function calling is in scope); `response_format`/structured output is accepted-but-not-enforced (best-effort only, no enforcement claim). These are treated as confirmed facts, not re-litigated.
- **Inference params are exactly four**: temperature, max tokens, top_p, frequency_penalty — the only ones FL `ChatSettings` supports. `top_k`/`min_p`/`repeat_penalty`/`seed` do not exist in Foundry Local and are therefore absent by design (Constitution IV).
- **Loaded-model prerequisite**: a model loaded via M3 (through the `IModelStateGate`) is required; load-on-demand reuses that existing path and respects the gate's load/unload concurrency.
- **Pipeline shape**: the MEAI middleware pipeline is `adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()` as already documented in `ChatService`; M4 builds it out.
- **Markdown/code rendering** is done in the Blazor Hybrid WebView; the v1 tool surface is intentionally minimal (one or two genuine example tools).
- **Context-window counting is approximate**: token estimation against a model's context length is an estimate by nature and is always presented as such; exact server-side tokenization is not assumed.
- **Persistence location**: conversations are stored in the app's local data directory (consistent with how M3 settings/cache config persist); folders for organizing chats are optional and may be deferred without affecting definition of done.
- **Regenerate-last and structured output are optional (P3)**; full branching/regenerate trees, RAG, voice, presets, MCP, vision input, and the local server are explicitly out of scope for M4.
- **Verification path**: per KI-001, DOM inspection via DevFlow is the sanctioned autonomous evidence path; pixel screenshots require the window frontmost. Pure seams are verified by dylib-free unit tests.
- **No sync-over-async**: per KI-005, the entire chat/streaming/cancellation path is fully async with no `.Result`/`.Wait()`.
