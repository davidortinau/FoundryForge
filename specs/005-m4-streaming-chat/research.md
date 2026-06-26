# Phase 0 Research: M4 — Chat experience (v1 core)

All decisions below are grounded in real code, the referenced MEAI 10.0.1 surface, `docs/DESIGN.md`, the M0d feasibility finding, and the milestone references (Constitution I). Open **runtime** unknowns resolvable only on Apple-Silicon hardware are flagged explicitly and pinned for the DevFlow e2e — they are not guessed.

---

## R1 — Markdown + code-copy rendering library

**Question**: How to render assistant markdown (headings, lists, emphasis, inline + fenced code) with a per-block Copy control, dependency-light and safe against HTML injection from model output (FR-005)?

**Evidence**: `Directory.Packages.props` already pins **`Markdig 0.44.0`** in the active `net11-primary` group (currently unreferenced by any project). Markdig is a pure managed CommonMark library — no native dependency — so it can be referenced by `FoundryStudio.Core` and stays unit-testable in the Core-only test project.

**Decision**: Add a `PackageReference` to **`FoundryStudio.Core`** for the already-pinned Markdig `0.44.0` (refresh `packages.lock.json` for Core; no new pin needed). Implement a pure Core seam `ChatMarkdown` that:
- builds a `MarkdownPipeline` with **`.DisableHtml()`** so raw HTML in model output is encoded, not rendered (the primary injection defense, FR-005), plus `.UseAdvancedExtensions()` only as needed for tables/task-lists;
- renders to an HTML string for the Blazor `MarkupString`;
- **extracts each fenced code block's exact raw text + language** (via the parsed `MarkdownDocument` / `FencedCodeBlock` AST) so the UI can attach a Copy control that copies the *exact* code (not a re-serialized/HTML-escaped variant) (FR-005, US1.4).

The Blazor `MessageBubble` consumes `ChatMarkdown` output; copy is wired with a tiny `wwwroot` JS clipboard helper (mirrors the existing `foundryStudioDialog` JS pattern). Streaming renders progressively: each partial update re-renders markdown for the in-progress assistant turn (incremental, real-token-driven, never a typewriter animation).

- **Rationale**: Markdig is already pinned/known-good, dependency-light, AST-accessible (clean code-block extraction), and `DisableHtml()` is the sanctioned safe-render path. Keeping it in Core makes markdown/code-block detection a dylib-free unit-testable seam (FR-038, SC-002).
- **Alternatives rejected**: a hand-rolled minimal renderer (more code, weaker correctness, easy to mis-sanitize); rendering raw model HTML (injection risk — violates FR-005); a JS markdown lib in the WebView (moves sanitization/code-extraction out of the testable Core seam).
- **Lock-file note**: adding the Core `PackageReference` requires a `dotnet restore` so `FoundryStudio.Core/packages.lock.json` records Markdig; call this out in the first implementation task.

---

## R2 — Honest token metrics from `ChatResponseUpdate` (TTFT, tok/sec, total, stop reason)

**Question**: Where do real metrics come from, and what happens when the engine doesn't provide them (FR-014/015/016)?

**Evidence**: MEAI `ChatResponseUpdate` carries `Contents` (which may include a **`UsageContent`** with `UsageDetails.InputTokenCount`/`OutputTokenCount`/`TotalTokenCount`) and a **`FinishReason`** (`ChatFinishReason` — `Stop`/`Length`/`ContentFilter`/`ToolCalls`). Today `FoundryChatClient.GetStreamingResponseAsync` yields only `new ChatResponseUpdate(ChatRole.Assistant, token)` and the `FoundryMessageMapper` extracts only text — **usage and finish-reason are not yet surfaced**, and OpenAI-style streams emit terminal/usage frames with empty `Choices` (KI-007 #2).

**Decision**: Two parts.
1. **Foundry layer** (`FoundryChatClient` + `FoundryMessageMapper`): when the FL/Betalgo stream exposes usage (terminal/usage frame) and/or a finish reason, surface it on the yielded `ChatResponseUpdate` (a trailing update carrying `UsageContent` and `FinishReason`). When FL provides neither, **emit nothing fabricated** — the metric stays unknown. (Whether FL/Betalgo currently populates usage/finish-reason per stream is a runtime unknown — see PIN below.)
2. **Core pure seam** `TokenStatsAccumulator`: deterministic, clock-injected. `OnSend(t0)`; `OnUpdate(update, tNow)` (counts the first text update for **TTFT = tFirst − t0**, accumulates token/char count + last timestamp); `Complete(finishReason, usage?)` → `GenerationMetrics`:
   - **TTFT** = first-update arrival minus send (always real, since we always observe the first token);
   - **tokens/sec** = observed token count ÷ elapsed (honest, derived from real timing/counts);
   - **total tokens** = `UsageContent.TotalTokenCount` when present, else **honest "unknown"** (never back-computed-and-presented-as-exact);
   - **stop reason** = mapped from `ChatFinishReason` (natural `Stop`, `Length`→max-tokens, `ToolCalls`); a user Stop (US3) overrides to **user-cancelled**; an error → error; absent → **unknown**.
   `GenerationMetrics` uses nullable fields so absent = honest "unknown" in the UI (DESIGN §8 mono tertiary row).

- **Rationale**: TTFT and tok/sec are derivable from timing we always observe; total tokens depends on engine-provided usage and must degrade to "unknown" honestly (Constitution III, FR-016). A clock-injected accumulator is fully unit-testable over a synthetic update sequence (SC-005).
- **PIN for DevFlow e2e**: on hardware, observe whether FL/Betalgo populates `UsageContent`/total tokens and a finish reason. Record the observed behavior in the `Verified:` line; if usage is never provided, the row honestly shows total = "unknown" (no fabrication) — that is a passing, honest outcome.
- **Alternatives rejected**: counting tokens by splitting text and presenting it as the engine's exact total (violates FR-016); animating a fake tok/sec (violates FR-035).

---

## R3 — Chat-history persistence format & store placement

**Question**: How is conversation history persisted so it survives restart, stays user-editable/auditable, and supports consent-gated destructive ops (FR-023/025/027, Constitution IV)?

**Evidence**: `FileSettingsService` is the established pattern — human-readable JSON via `SettingsDocument`, an `SemaphoreSlim` IO guard, consent-gated reset, and **non-destructive recovery** (an unparseable file is preserved as `.bak`, defaults returned, never silently overwritten). `ConfirmDialog.razor` + `ConsentGuard` are the consent precedent. `FileSystem.AppDataDirectory` is injected in `MauiProgram` (the App supplies the path, keeping MAUI out of the managed layer).

**Decision**:
- **Format**: one **human-readable JSON file per conversation** under `<AppData>/chats/<id>.json` (plus the conversation's system prompt + the four params inside the same document). Per-file (not one monolith) keeps each conversation independently writable, limits blast radius, and makes a single delete a single file removal.
- **Serialization in Core**: `ChatHistoryDocument` (mirrors `SettingsDocument`) owns `Serialize`/`Deserialize` of `ChatSession` with `System.Text.Json` (indented, stable property names, a `SchemaVersion`). Round-trip is the unit-tested seam (SC-008).
- **Store**: a new Core abstraction `IChatHistoryStore` with a Core file impl **`FileChatHistoryStore`** (the App injects the `chats` directory path in its constructor). It implements list/load/save and a **consent-gated** delete/clear (`userConfirmed:false` ⇒ no-op/throw, removes nothing — the dylib-free enforcement point, SC-009). Parse-failure preserves `.bak` (no silent wipe). Writes are atomic-ish (write temp, replace) and serialized via a `SemaphoreSlim`.
- **Placement rationale**: `FileChatHistoryStore` is plain managed file IO (no FL, no MAUI — the path is injected), so placing it in **`.Core`** makes it directly unit-testable in the Core-only test project (temp dir) — a deliberate, documented refinement over `FileSettingsService`'s historical Foundry-layer placement. The behavior pattern (human-readable JSON, consent gate, `.bak` recovery, App-injected path) mirrors `FileSettingsService` exactly.
- **Interrupted turns**: an in-flight/stopped turn is persisted with its real state (partial text, stop reason = user-cancelled/error) and is **never** written as a clean completion (FR-027); persistence happens after each completed/stopped turn and on param/system-prompt change.

- **Alternatives rejected**: a single monolithic `chats.json` (larger blast radius, whole-file rewrites, harder partial recovery); a database (over-engineered for v1, not human-readable/auditable — violates the Constitution IV auditability gate); storing MEAI `ChatMessage` objects directly (not cleanly serializable/stable — use the Core `ChatMessageRecord` and map to MEAI at request time).

---

## R4 — MEAI middleware pipeline build (`UseFunctionInvocation` + `UseOpenTelemetry`)

**Question**: How is the documented pipeline `adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()` actually constructed, and where do tools attach (FR-028)?

**Evidence**: `ChatService` today wraps the raw `FoundryChatClient` directly with a comment marking exactly this seam: `// M4 seam: adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()`. MEAI 10.0.1 provides `ChatClientBuilder` (`AsBuilder()`), `UseFunctionInvocation()` (executes `AIFunction` tool calls automatically), and `UseOpenTelemetry()` (gen-AI semantic conventions — Constitution telemetry gate).

**Decision**: Build the pipeline once in `ChatService`'s constructor:
```csharp
_chatClient = adapter.AsBuilder()
    .UseFunctionInvocation()
    .UseOpenTelemetry()   // gen-AI semantic conventions; no PII in spans
    .Build();
```
Tools are supplied **per request** via `ChatOptions.Tools` (the `InferenceParameters.ToChatOptions(...)` seam accepts the registered tool list). The app-defined `AIFunction`s come from a small registered collection (DI) so `UseFunctionInvocation()` can execute them. `UseOpenTelemetry()` satisfies the Constitution telemetry gate (OpenTelemetry gen-AI conventions, no PII).

- **Rationale**: This is the exact seam the codebase reserved for M4; `UseFunctionInvocation` is MEAI's real, non-faked tool-execution middleware (FR-029).
- **PIN for DevFlow e2e**: confirm end-to-end tool invocation on a tool-capable loaded model (M0d found FL honors `tools`); record observed behavior.
- **Alternatives rejected**: hand-rolling tool-call dispatch (re-implements MEAI middleware, risks a faked path); skipping `UseOpenTelemetry` (misses the telemetry gate).

---

## R5 — Genuine example tools (no dead UI)

**Question**: What one or two *real*, working .NET tools ship in v1 (FR-030, Constitution IV)?

**Decision**: Two genuine, deterministic, local-only `AIFunction`s (via `AIFunctionFactory.Create`), defined in `FoundryStudio.Foundry/Tools/ChatTools.cs`:
1. **`get_current_time`** — returns the current local date/time (ISO 8601). Real, verifiable, no network.
2. **`get_loaded_model_info`** — returns the active model's alias + reported context length by calling `IFoundryCatalogService.ListLoadedAsync()` (real app state, genuinely useful in-conversation, and reinforces the honesty story).

Both actually execute and feed their real result back via `UseFunctionInvocation()`. Tool errors surface plainly (FR-029). No decorative tool affordance ships (FR-030); when no tool is needed, plain streaming chat behaves exactly as US1.

- **Rationale**: Both are real, side-effect-free, and demonstrate end-to-end local function calling without inventing capability. `get_loaded_model_info` exercises the M1–M3 service surface honestly.
- **Alternatives rejected**: a "search the web" tool (network, out of the offline lighthouse scope); any placeholder/no-op tool (dead UI, violates FR-030).

---

## R6 — Inference params → `ChatOptions` → FL request (exactly four, honest)

**Question**: How do the four real params flow into the request, and how is the absence of unsupported params enforced (FR-018/019)?

**Evidence**: MEAI `ChatOptions` has first-class `Temperature`, `MaxOutputTokens`, `TopP`, `FrequencyPenalty` — exactly the four FL `ChatSettings` supports — and notably **no** `top_k`/`min_p`/`repeat_penalty`/`seed` first-class properties. `FoundryChatClient` has a `TODO(M4)` to map options into the request; `FoundryMessageMapper` is the single Betalgo touchpoint.

**Decision**: A pure Core seam `InferenceParameters` (record of the four values) with `ToChatOptions(string modelId, IEnumerable<AITool>? tools)` that sets exactly those four `ChatOptions` properties (+ `ModelId` + `Tools`). The Foundry layer maps `ChatOptions` → the Betalgo request fields inside `FoundryMessageMapper` (the isolated FL touchpoint). The UI exposes **only** the four controls (`InferenceParamsPanel.razor`); there is no code path or DOM element for `top_k`/`min_p`/`repeat_penalty`/`seed` (FR-019, SC-006). Defaults are sensible and honest ranges are conveyed per control.

- **Rationale**: The four params are exactly MEAI's first-class set and FL's real set; the Core mapping is dylib-free unit-testable (SC-006), and the unsupported params are absent **by construction** (no property to set, no control to render).
- **Alternatives rejected**: stuffing unsupported params into `ChatOptions.AdditionalProperties` (would imply a capability FL lacks — violates FR-019/Constitution IV).

---

## R7 — Context-window estimate (honest approximation)

**Question**: How to show context usage vs the model's context length as an explicit estimate, with an unknown-context case (FR-020/021/022)?

**Evidence**: `ModelInfo.ContextLength` is a nullable `int?` (null = not reported — honest unknown precedent). Exact server-side tokenization is not assumed (Assumptions).

**Decision**: A pure Core seam `ContextWindowEstimator.Estimate(int usedTokensEstimate, int? contextLength, double warnFraction = 0.8)` → `ContextWindowEstimate { UsedTokensEstimate, ContextLength?, Fraction?, IsWarn, IsUnknown }`. Token usage is **approximated** (a documented chars≈token/4 heuristic over the transcript) and always labeled an **estimate** in the UI (DESIGN, never "exact"). When `contextLength is null`, `IsUnknown = true`, `Fraction = null` — the UI says the limit is unknown and **fabricates no denominator/percentage** (FR-022). `IsWarn` when `Fraction >= warnFraction`.

- **Rationale**: Token counting against context length is inherently approximate; presenting it as an estimate (and "unknown" when context length is unreported) is the only honest framing (Constitution III). Pure ⇒ unit-testable (SC-007).
- **Alternatives rejected**: a precise-looking percentage from a heuristic (false exactness); inventing a default context length when unreported (fabricated denominator — violates FR-022).

---

## R8 — Cancellation (clean, cooperative, no sync-over-async)

**Question**: How does Stop halt generation while preserving partial text and avoiding `.Result`/`.Wait()` (FR-011/013, KI-005)?

**Evidence**: `FoundryChatClient.GetStreamingResponseAsync` already threads `[EnumeratorCancellation] CancellationToken` into the FL stream and takes a generation lease via `await using var lease = await _gate.BeginGenerationAsync(...)`. KI-005 forbids sync-over-async; the `NoBlockingInitGuard` test enforces the no-blocking discipline.

**Decision**: The Chat surface owns a per-generation `CancellationTokenSource`; Stop calls `Cts.Cancel()`. The stream is consumed with `await foreach` (off the dispatcher), and on `OperationCanceledException` the partial assistant text already accumulated is preserved as a **stopped** turn (stop reason = user-cancelled), the surface returns to ready, and the lease disposes (`await using`) so no orphaned generation continues. **No** `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` anywhere; UI re-renders via `await InvokeAsync(StateHasChanged)`. Only one active generation per conversation (the composer disables/queues a concurrent send honestly).

- **Rationale**: The token + lease are already plumbed; clean cooperative cancellation is the correct, KI-005-compliant path. The cancellation wiring is unit-testable over a controllable fake stream (SC-004).
- **PIN for DevFlow e2e**: confirm Stop halts promptly on hardware and the partial turn is preserved; confirm no background generation continues (lease released).
- **Alternatives rejected**: blocking on the cancel (violates KI-005); discarding partial text (violates FR-011).

---

## R9 — Loaded-model gate & load-on-demand

**Question**: How does Chat gate on a loaded model and offer load-on-demand without bypassing the gate (FR-007/008/009/010)?

**Evidence**: `IFoundryCatalogService.ListLoadedAsync()` is the authoritative loaded set; `LoadAsync(alias, variantId?, ct)` runs through the M1 `IModelStateGate`; `ModelBusyException` is thrown when a mutation is rejected. `FoundryChatClient` also calls `LoadAsync` defensively before streaming.

**Decision**: `Chat.razor` reads `ListLoadedAsync()` on open. If none loaded, render `ChatGate.razor` (honest "a model must be loaded", offer to load a cached model via `LoadAsync` through the gate) — the composer does not accept a send. On successful load the surface becomes ready and names the active model. If a model is already loaded, the surface is immediately ready. A `ModelBusyException` surfaces as an honest "model busy, try again" (no hang). If a model is unloaded elsewhere while Chat is open, the surface reflects not-ready (re-check loaded state / honest send rejection). The ready/not-ready gating is a unit-testable pure decision over the loaded set.

- **Rationale**: Reuses the existing gate/load path (FR-008, Constitution V); honest gating prevents a dead composer (Constitution IV).
- **Alternatives rejected**: loading outside the gate (Constitution V breach); a silent fake-ready composer (violates FR-007).

---

## Summary of decisions

| # | Decision | Rationale | Key alternative rejected |
|---|----------|-----------|--------------------------|
| R1 | Markdig 0.44.0 (already pinned) in Core; `DisableHtml()` + AST code-block extraction | dependency-light, safe, testable seam | hand-rolled / raw-HTML render |
| R2 | Surface usage/finish-reason from stream; clock-injected `TokenStatsAccumulator`; absent ⇒ "unknown" | honest metrics, unit-testable | fabricated/back-computed totals |
| R3 | Per-conversation JSON under `<AppData>/chats/`; `ChatHistoryDocument` + `FileChatHistoryStore` in Core; consent gate; `.bak` recovery | mirrors FileSettingsService, auditable, testable | monolith / database |
| R4 | Build `AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()` in ChatService; tools via `ChatOptions.Tools` | the reserved seam; real MEAI middleware | hand-rolled tool dispatch |
| R5 | Two genuine tools: `get_current_time`, `get_loaded_model_info` | real, local, no dead UI | network/placeholder tools |
| R6 | `InferenceParameters.ToChatOptions(...)` sets exactly 4 props; unsupported absent by construction | honest param surface, testable | AdditionalProperties for unsupported |
| R7 | `ContextWindowEstimator` — chars≈token/4 estimate; null context ⇒ unknown, no denominator | only honest framing | false-exact % / invented denominator |
| R8 | Per-gen `CancellationTokenSource` + `await foreach`; preserve partial; lease disposes; no `.Result`/`.Wait()` | KI-005-clean, token already plumbed | blocking cancel / discard partial |
| R9 | Gate on `ListLoadedAsync`; load-on-demand via `LoadAsync` through the gate; honest busy/not-ready | reuses gate, no dead composer | load outside gate / fake-ready |

**Runtime unknowns pinned for the Apple-Silicon DevFlow e2e** (recorded in the `Verified:` line, not guessed): FL/Betalgo emission of usage/total-tokens and finish-reason (R2); FL mid-stream cancel honoring (R8); end-to-end tool invocation on a tool-capable model (R4/R5). Each has an honest fallback (metric "unknown"; authoritative re-check; plain error) so M4 stays honest regardless of observed behavior.
