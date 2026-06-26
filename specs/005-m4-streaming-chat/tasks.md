---
description: "Task list for M4 — Chat experience (v1 streaming chat)"
---

# Tasks: M4 — Chat experience (v1 core)

**Input**: Design documents from `/specs/005-m4-streaming-chat/`

**Prerequisites**: plan.md (Constitution PASS; concrete file layout — extend the 4 existing projects, no new projects), spec.md (8 in-scope user stories 4×P1/4×P2 + 1 optional P3, FR-001–040, SC-001–013), research.md (Markdig R1 / usage+finish-reason R2 / persistence R3 / pipeline R4 / tools R5 / params R6 / context-estimate R7 / cancellation / evidence), data-model.md (ChatSession, ChatMessageRecord, InferenceParameters, GenerationMetrics, ContextWindowEstimate, IChatHistoryStore, ChatHistoryDocument), contracts/ (core-seams.md, service-surface.md, chat-ui.dom.md), quickstart.md (Layer A dylib-free unit tests + Layer B Apple-Silicon DevFlow DOM e2e; data-preservation-safe DoD).

**Tests**: M4 ships **dylib-free xUnit** in `tests/FoundryStudio.Tests` for **every** Core seam (TranscriptAssembler, InferenceParameters→ChatOptions mapping, TokenStatsAccumulator, ContextWindowEstimator, ChatMarkdown sanitize/extract, ChatHistoryDocument round-trip, the `IChatHistoryStore` consent gate) **plus** a tool-invocation wiring test (fake `IChatClient` through `UseFunctionInvocation()`). All UI behavior is verified via **DevFlow DOM inspection** on real Apple Silicon (KI-001 — the sanctioned autonomous evidence path; `ui screenshot` needs the window frontmost and is a light/dark supplement, never the gate) in the close phase.

**Organization (matches how we implement)**: Core dylib-free seams + their unit tests land **first** (coordinator-authored, unit-testable in the Core-only test project) → then the **Foundry-layer wiring** (MEAI pipeline + options/usage/tools, coordinator/Bishop) → then the **Blazor chat UI** grouped by user story (Hicks) → then **verification** (Layer A unit suites + Layer B DevFlow DOM e2e with light/dark screenshots) and the data-preservation-safe DoD. Story labels (US1–US8) trace every task to its spec story.

## M4 Guardrails (apply to EVERY task)

- **Capability honesty (Constitution III/IV)**: every token/metric comes from the **real** stream; genuinely-absent values render honest **"unknown"/"estimate"/"unsupported"** — never fabricated, guessed-as-exact, or typewriter-animated (FR-003/016/022/035). **Only** temperature / max-tokens / top_p / frequency_penalty are surfaced — **zero** controls for `top_k`/`min_p`/`repeat_penalty`/`seed` (FR-019). Structured output is best-effort pass-through with **no** enforcement claim/toggle (FR-031). Tools are genuine — **no** dead/decorative tool UI (FR-030).
- **Data preservation (Constitution IV, load-bearing)**: chat history is user data — persisted to app data, survives restart (FR-023/027). Clear/delete only behind the reused M3 `ConfirmDialog` (names the exact conversation, "cannot be undone", **Cancel** default-focused); a single activation only *opens* the dialog (never deletes) (FR-025/026). `DeleteAsync`/`ClearMessagesAsync(userConfirmed:false)` **remove nothing** — the dylib-free enforcement point (SC-009). Unparseable history file ⇒ preserved as `.bak`, never silently wiped. An interrupted/stopped turn persists its real partial content + stop reason, never as a clean completion (FR-027).
- **Layering (Constitution V / DEC-004)**: `FoundryStudio.App` consumes **only** `IChatService`, `IChatHistoryStore`, `IFoundryCatalogService`, `IModelStateGate`, and `FoundryStudio.Core` seams + MEAI types — **never** `Microsoft.AI.Foundry.Local`/Betalgo (FR-034). All FL/Betalgo types stay inside `FoundryStudio.Foundry`.
- **In-process + concurrency (Constitution V)**: chat runs in-process via `IChatService.StreamAsync` over `FoundryChatClient` — **no loopback** socket (FR-002). Every stream takes a generation lease from the single `IModelStateGate`; `ModelBusyException` is surfaced honestly, never hangs (FR-010).
- **KI-005 (NON-NEGOTIABLE)**: **no** `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` anywhere in the chat path; the stream is consumed with `await foreach` off the dispatcher and re-rendered via `await InvokeAsync(StateHasChanged)`, never blocking the BlazorWebView dispatcher (FR-037). The `NoBlockingInitGuard` test stays green.
- **Seam purity**: the Core seams stay FL-free and dylib-free so the CI seam gate stays green. The **only** package delta is adding a `PackageReference` to the already-pinned **Markdig 0.44.0** in `.Core` (R1) — refresh `packages.lock.json` then.
- **Accessibility (FR-036)**: every new control is labeled, keyboard-reachable, state changes perceivable, never color-alone; the streamed region is `aria-live="polite"`; WCAG AA in **both** Workshop Daylight and Night Forge.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: maps the task to its user story (US1–US8). Setup / Foundational / Foundry-wiring / UI-scaffold / Polish carry no story label.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the M4 baseline and land the one package delta before any code changes.

- [ ] T001 Confirm a green baseline: build the solution and run `dotnet test tests/FoundryStudio.Tests/FoundryStudio.Tests.csproj` from repo root; record the existing-suite pass set (incl. `NoBlockingInitGuard`, `RamFitHeuristicTests`, `ModelStateGateTests`, `SettingsDocumentTests`, `CatalogGroupingTests`, `DeleteConsentGateTests`, …) so regressions are visible.
- [ ] T002 Add a `<PackageReference Include="Markdig" />` to `src/FoundryStudio.Core/FoundryStudio.Core.csproj` (version comes from the already-pinned `Markdig 0.44.0` in `Directory.Packages.props` — do **not** re-pin), then `dotnet restore` to refresh `src/FoundryStudio.Core/packages.lock.json` (R1). This is the **only** new dependency in M4; confirm no other `packages.lock.json` changes. Markdig is fully managed ⇒ Core stays dylib-free.

**Checkpoint**: Baseline green; Markdig referenced in Core; lock file refreshed.

---

## Phase 2: Foundational — Core data models & abstraction (Blocking Prerequisites) — coordinator

**Purpose**: The pure record/DTO types + the persistence abstraction that the Core seams, the Foundry wiring, and the UI all consume. FL-free, dylib-free. These MUST land before the seam-logic phase so the seam test+impl pairs can be staffed in parallel.

**⚠️ CRITICAL**: No seam-logic or UI work begins until this phase is complete.

- [ ] T003 [P] Create `src/FoundryStudio.Core/Chat/InferenceParameters.cs` — `sealed record InferenceParameters(double? Temperature = null, int? MaxOutputTokens = null, double? TopP = null, double? FrequencyPenalty = null)` with `static InferenceParameters Defaults { get; }` and `ChatOptions ToChatOptions(string modelId, IEnumerable<AITool>? tools = null)`. The mapping sets **exactly** `Temperature`/`MaxOutputTokens`/`TopP`/`FrequencyPenalty`/`ModelId`/`Tools` and **nothing** for `top_k`/`min_p`/`repeat_penalty`/`seed` (no such property exists — FR-019); a `null` param leaves the property unset (engine default, no fabricated value, FR-018). Pure, no FL, no I/O. (Mapping proven by T012; consumed by `ChatSession`.)
- [ ] T004 [P] Create `src/FoundryStudio.Core/Models/GenerationMetrics.cs` — `sealed record GenerationMetrics(TimeSpan? TimeToFirstToken, double? TokensPerSecond, int? TotalTokens, StopReason StopReason)` + `enum StopReason { Natural, MaxTokens, ToolCalls, UserCancelled, Error, Unknown }`. Each nullable metric = honest "unknown" when genuinely unavailable (FR-016).
- [ ] T005 [P] Create `src/FoundryStudio.Core/Models/ContextWindowEstimate.cs` — `sealed record ContextWindowEstimate(int UsedTokensEstimate, int? ContextLength, double? Fraction, bool IsWarn, bool IsUnknown)`. The `…Estimate` name encodes that the figure is always presented as an estimate (FR-020); `ContextLength == null` ⇒ `IsUnknown` with `Fraction == null` (no fabricated denominator, FR-022).
- [ ] T006 [US7] Create `src/FoundryStudio.Core/Models/ChatMessageRecord.cs` — `sealed record ChatMessageRecord(ChatTurnRole Role, string Content, DateTimeOffset CreatedAt, GenerationMetrics? Metrics = null, StopReason? StopReason = null)` + `enum ChatTurnRole { System, User, Assistant, Tool }`. A stopped/interrupted assistant turn stores its real partial `Content` + `StopReason = UserCancelled`/`Error`, never a clean completion (FR-027) (depends on T004).
- [ ] T007 [US7] Create `src/FoundryStudio.Core/Models/ChatSession.cs` — `sealed record ChatSession(string Id, string Title, string? SystemPrompt, InferenceParameters Parameters, string? ModelAlias, IReadOnlyList<ChatMessageRecord> Messages, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int SchemaVersion = 1)`. `Id` is a stable guid (the file name / delete-clear target); `Title` defaults non-destructively from the first user message; `SystemPrompt`/`Parameters` persist and survive restart (FR-017/023) (depends on T003, T006).
- [ ] T008 [P] [US7] Create `src/FoundryStudio.Core/Abstractions/IChatHistoryStore.cs` — `ListAsync(ct)`, `GetAsync(id, ct)`, `SaveAsync(session, ct)`, `DeleteAsync(id, bool userConfirmed, ct)`, `ClearMessagesAsync(id, bool userConfirmed, ct)`. The `userConfirmed` flag is the dylib-free consent enforcement point (Constitution IV, FR-026); mirrors the `ISettingsService` precedent (depends on T007).

**Checkpoint**: Core records + persistence abstraction compile; seam-logic pairs can now proceed in parallel.

---

## Phase 3: Core pure seams + dylib-free unit tests (CI seam gate) — coordinator

**Purpose**: The FL-free, dylib-free logic seams and their xUnit coverage — the heart of M4's honesty guarantees, unit-testable in the Core-only project (FR-038). **TDD**: each test is written to **FAIL first**, then its seam makes it green.

**⚠️ CRITICAL**: These seams + tests are the Constitution-II evidence backbone; every UI and Foundry task downstream consumes them.

- [ ] T009 [P] [US1] Create `src/FoundryStudio.Core/Chat/TranscriptAssembler.cs` — `static IReadOnlyList<ChatMessage> Assemble(ChatSession session)`: non-empty `SystemPrompt` ⇒ first message `new ChatMessage(ChatRole.System, …)` (FR-017), empty/null ⇒ none; each `ChatMessageRecord` maps **in order** (`ChatTurnRole`→`ChatRole`, tool turns → `ChatRole.Tool`) for full multi-turn context (FR-004); never drops/reorders; pure, deterministic, dylib-free (depends on T007).
- [ ] T010 [P] [US1] Create dylib-free `tests/FoundryStudio.Tests/TranscriptAssemblerTests.cs` (SC-001): system + 2 user/assistant pairs → correct order & roles; no system prompt → no system message; a tool turn preserved. Write to FAIL first, then confirm green against T009.
- [ ] T011 [P] [US4] Create `src/FoundryStudio.Core/Chat/TokenStatsAccumulator.cs` — `void OnSend(DateTimeOffset sentAt)`, `void OnUpdate(ChatResponseUpdate update, DateTimeOffset arrivedAt)`, `GenerationMetrics Complete(StopReason stopReason)`. **TTFT** = first-update `arrivedAt − sentAt` (real; null only if no update arrived); **TokensPerSecond** = observed segments ÷ elapsed (null if not derivable); **TotalTokens** = `UsageContent.Details.TotalTokenCount` when a `UsageContent` was observed, else **`null` = honest "unknown"** (never back-computed-as-exact, FR-016); **StopReason** = the `Complete` argument. Clock-injected (timestamps passed in) ⇒ deterministic; no real-clock/IO (depends on T004).
- [ ] T012 [P] [US4] Create dylib-free `tests/FoundryStudio.Tests/TokenStatsAccumulatorTests.cs` (SC-005): synthetic timestamped updates → TTFT & tok/sec computed; a trailing `UsageContent` → real total; **no** usage ⇒ `TotalTokens == null`; `Complete(UserCancelled)` → stop reason honored. Write to FAIL first.
- [ ] T013 [P] [US5] Create dylib-free `tests/FoundryStudio.Tests/InferenceParametersMappingTests.cs` (SC-006): each of the four params flows into the matching `ChatOptions` property; a `null` param leaves it unset; `ChatOptions` exposes **no** surface for `top_k`/`min_p`/`repeat_penalty`/`seed`; supplied `tools` attach to `ChatOptions.Tools` (for `UseFunctionInvocation()`). Proves the FR-019 honesty invariant dylib-free against T003. Write to FAIL first.
- [ ] T014 [P] [US6] Create `src/FoundryStudio.Core/Chat/ContextWindowEstimator.cs` — `static ContextWindowEstimate Estimate(int usedTokensEstimate, int? contextLength, double warnFraction = 0.8)`: `contextLength is null` ⇒ `IsUnknown=true`, `Fraction=null`, `IsWarn=false` (no fabricated denominator, FR-022); known length ⇒ `Fraction = used/length`, `IsWarn = Fraction >= warnFraction` (FR-021). Pure, deterministic; the chars≈token/4 approximation is the caller's input, documented as an estimate (depends on T005).
- [ ] T015 [P] [US6] Create dylib-free `tests/FoundryStudio.Tests/ContextWindowEstimatorTests.cs` (SC-007): usage advances the fraction; crossing `warnFraction` ⇒ `IsWarn`; null context ⇒ `IsUnknown` + null fraction; zero usage ⇒ fraction 0. Write to FAIL first.
- [ ] T016 [P] [US1] Create `src/FoundryStudio.Core/Chat/ChatMarkdown.cs` — `static RenderedMarkdown Render(string markdown)` + `readonly record struct CodeBlock(string Language, string Code)` + `sealed record RenderedMarkdown(string Html, IReadOnlyList<CodeBlock> CodeBlocks)`. Render via a Markdig pipeline built with **`.DisableHtml()`** so raw model HTML is encoded, not executed (HTML-injection defense, FR-005); `CodeBlocks` carries each fenced block's **exact raw code** + language from the AST so the UI Copy control copies the literal code. Pure, dylib-free (Markdig is managed) (depends on T002).
- [ ] T017 [P] [US1] Create dylib-free `tests/FoundryStudio.Tests/ChatMarkdownTests.cs` (SC-002): headings/lists/emphasis/inline code render; a fenced `csharp` block appears in `CodeBlocks` with exact code + language `csharp`; a `<script>`/raw-HTML input is **encoded** in `Html` (not active markup). Write to FAIL first.
- [ ] T018 [P] [US7] Create `src/FoundryStudio.Core/Chat/ChatHistoryDocument.cs` — `Serialize`/`Deserialize` a `ChatSession` to/from human-readable JSON (mirrors `SettingsDocument`), preserving every field incl. `SchemaVersion` (SC-008); a malformed document is recoverable (no throw out of read) so the store can preserve it as `.bak`. Pure, dylib-free (depends on T007).
- [ ] T019 [P] [US7] Create dylib-free `tests/FoundryStudio.Tests/ChatHistoryDocumentTests.cs` (SC-008): a `ChatSession` with system prompt + the four params + multi-turn messages round-trips field-for-field; garbage JSON is recoverable. Write to FAIL first.
- [ ] T020 [US7] Create `src/FoundryStudio.Core/Chat/FileChatHistoryStore.cs` — `IChatHistoryStore` file impl (App injects the `chats` directory path; keeps MAUI/`FileSystem` out of Core). `SaveAsync` upserts one `<id>.json` per conversation (survives restart, FR-023/027); `ListAsync`/`GetAsync` reload from disk; **`DeleteAsync`/`ClearMessagesAsync` guard on `userConfirmed` before any file mutation — `false` removes nothing** (Constitution IV enforcement, FR-026); an unparseable file is preserved as `.bak`, never silently wiped. Plain managed file IO ⇒ unit-testable with a temp dir (depends on T008, T018).
- [ ] T021 [US7] Create dylib-free `tests/FoundryStudio.Tests/ChatHistoryConsentGateTests.cs` (SC-009): against a temp-dir store, `DeleteAsync(id, userConfirmed:false)` and `ClearMessagesAsync(id, false)` **remove nothing** (file still present, messages intact); `userConfirmed:true` removes. Proves the consent gate without any FL/dylib (depends on T020). Write to FAIL first.
- [ ] T022 [P] [US8] Create dylib-free `tests/FoundryStudio.Tests/ToolInvocationWiringTests.cs` (SC-010): a fake `IChatClient` scripted to emit a tool call, wrapped with `AsBuilder().UseFunctionInvocation().Build()`, invokes a **real** test `AIFunction` (`AIFunctionFactory.Create(...)`) and feeds its result back into the response — proving invocation is genuine MEAI middleware, not faked (FR-029). No FL, no dylib. Write to FAIL first.

**Checkpoint**: All eight dylib-free seam suites green (CI seam gate clean) — every honesty/consent invariant unit-proven before any FL or UI touches it (Quickstart Layer A; SC-001/002/005/006/007/008/009/010).

---

## Phase 4: Foundry-layer wiring (MEAI pipeline + options/usage/tools) — coordinator / Bishop

**Purpose**: Build the MEAI middleware pipeline at the documented seam, map the four params + tools into the FL request, surface real usage/finish-reason from the stream, and supply the genuine example tools. **All FL/Betalgo types stay inside `FoundryStudio.Foundry`** (FR-034). The generation lease + cancellation threading are unchanged (Constitution V, KI-005).

- [ ] T023 [P] [US8] Create `src/FoundryStudio.Foundry/Tools/ChatTools.cs` — `static IReadOnlyList<AITool> Create(IServiceProvider sp)` returning **two genuine** `AIFunction`s (R5): `get_current_time` → current local ISO-8601 datetime (real, no network); `get_loaded_model_info` → active model alias + context length via `IFoundryCatalogService.ListLoadedAsync()` (real app state). No decorative/do-nothing tools (FR-030).
- [ ] T024 [US8] Build the MEAI pipeline in `src/FoundryStudio.Foundry/ChatService.cs` constructor at the documented seam (R4): `_chatClient = adapter.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build();` (replacing `_chatClient = adapter;`). `StreamAsync` keeps delegating to `_chatClient.GetStreamingResponseAsync(messages, options, ct)` — now through the pipeline; tools arrive per-request via `options.Tools` (set by `InferenceParameters.ToChatOptions`) so `UseFunctionInvocation()` executes them for real (FR-028/029). `UseOpenTelemetry()` emits gen-AI semantics with no PII. No loopback, no second manager; existing lease unchanged.
- [ ] T025 [US5] In `src/FoundryStudio.Foundry/Internal/FoundryMessageMapper.cs`, map `ChatOptions` → the FL/Betalgo request (resolving `FoundryChatClient.cs` `TODO(M4)` at L65): set temperature/max-tokens/top_p/frequency_penalty; attach tools; pass `response_format` **best-effort only** (no enforcement claim/toggle, FR-031). Unsupported params (`top_k`/`min_p`/`repeat_penalty`/`seed`) are **never** written — `ChatOptions` has no property for them (FR-019). All Betalgo types stay isolated here (FR-034) (depends on T024).
- [ ] T026 [US4] In `src/FoundryStudio.Foundry/FoundryChatClient.cs`/`FoundryMessageMapper.cs`, surface real telemetry from the stream: when the FL/Betalgo stream exposes a usage/terminal frame (KI-007 #2 empty-`Choices` frame), yield a trailing `ChatResponseUpdate` carrying `UsageContent`; when absent, **emit nothing** so the metric stays honest "unknown" (FR-016). Map the FL finish reason to `ChatResponseUpdate.FinishReason` (`ChatFinishReason`) when provided, else leave unset (FR-015). **PIN (hardware)**: whether FL populates usage/finish-reason is a runtime unknown (R2) — verified on Apple Silicon in T053; honest "unknown" is a passing fallback (depends on T025).

**Checkpoint**: In-process pipeline executes real tool calls and threads the four params; usage/finish-reason are surfaced honestly or omitted. FL stays behind the seam.

---

## Phase 5: UI scaffolding — nav activation, page shell, DI (Blocking for UI stories) — Hicks

**Purpose**: Activate the sidebar Chat nav, stand up the `/chat` page + view-state, and register the new services. These block the per-story UI phases.

- [ ] T027 [US1] In `src/FoundryStudio.App/Components/Layout/Sidebar.razor`, activate the Chat nav: turn the disabled "Coming soon" placeholder into an **enabled** `NavLink href="/chat"` with `id="nav-chat"` `data-testid="nav-chat"` — remove `disabled`/`aria-disabled`/"Coming soon" (FR-001).
- [ ] T028 [US1] Register the new services in `src/FoundryStudio.App/MauiProgram.cs`: `services.AddSingleton<IChatHistoryStore>(_ => new FileChatHistoryStore(Path.Combine(FileSystem.AppDataDirectory, "chats")));` (App injects the path — mirrors the `FileSettingsService` registration) and `services.AddSingleton<IReadOnlyList<AITool>>(sp => ChatTools.Create(sp));`. **No** `using Microsoft.AI.Foundry.Local`/Betalgo in `.App` (FR-034) (depends on T020, T023).
- [ ] T029 [US1] Create `src/FoundryStudio.App/Components/Pages/Chat.razor` (route `/chat`, `data-testid="chat-page"`) + a `ChatViewState` (transient, not persisted): holds the active `ChatSession`, the per-generation `CancellationTokenSource`, streaming flag, `TokenStatsAccumulator`, current `InferenceParameters`, and the loaded-set snapshot. Orchestrates the gate, composer, message list, metrics, context meter, params panel, and conversation list. Injects only `IChatService`, `IChatHistoryStore`, `IFoundryCatalogService`, `IModelStateGate`, Core seams, and the tools collection (FR-034) (depends on T027, T028).

**Checkpoint**: `nav-chat` enabled; `/chat` renders `chat-page`; DI wired. UI stories can proceed.

---

## Phase 6: User Story 1 — Multi-turn streaming chat with markdown + code-copy (Priority: P1) 🎯 MVP — Hicks

**Goal**: Send a message and watch the assistant reply render **incrementally** from the real `IAsyncEnumerable<ChatResponseUpdate>` stream, multi-turn, with markdown + per-code-block Copy, off the dispatcher (KI-005).

**Independent Test (DevFlow DOM)**: With a loaded model, send a message; assert `chat-msg-assistant` carries `data-streaming="true"` with **multiple successive partial renders** then `false` (never a single post-hoc paste, never a typewriter-only animation); send a follow-up and assert prior turns are included as context; on a reply with a fenced block assert `chat-codeblock` + `chat-code-copy` copies the **exact** code; assert raw HTML in a reply is inert.

- [ ] T030 [P] [US1] Create `src/FoundryStudio.App/Components/Chat/Composer.razor` — multiline input `data-testid="chat-composer"` + `<label>`, placeholder **"Ask this local model…"** (DESIGN §9); `data-testid="chat-send"` using the **Foundry Copper** accent (DESIGN §8), disabled on empty/whitespace (FR-006). Send invokes the page's `StreamAsync` flow (wired in T032).
- [ ] T031 [P] [US1] Create `src/FoundryStudio.App/Components/Chat/MessageList.razor` + `MessageBubble.razor` — ordered turns `data-testid="chat-messages"`; user turn `data-testid="chat-msg-user"`; assistant turn `data-testid="chat-msg-assistant"` + `data-streaming="true|false"` inside an `aria-live="polite"` region. `MessageBubble` renders assistant content via `ChatMarkdown.Render` (`data-testid="chat-markdown"`, HTML sanitized via `DisableHtml`), each fenced block as `data-testid="chat-codeblock"` with a `data-testid="chat-code-copy"` + `aria-label` Copy control that copies the **exact** `RenderedMarkdown.CodeBlocks` code; an awaiting state `data-testid="chat-awaiting"` ("pouring response") shows **no** fabricated partial text (FR-003/005/006) (depends on T016).
- [ ] T032 [US1] Wire the streaming send lifecycle in `Chat.razor`/`ChatViewState`: assemble the request via `TranscriptAssembler.Assemble(session)` and `InferenceParameters.ToChatOptions(modelId, tools)`; call `IChatService.StreamAsync(messages, options, cts.Token)` and consume it with `await foreach` **off the dispatcher**, re-rendering each update via `await InvokeAsync(StateHasChanged)` — **no** `.Result`/`.Wait()` (KI-005, FR-037); drive `TokenStatsAccumulator.OnSend/OnUpdate`; persist the completed turn via `IChatHistoryStore.SaveAsync` so the exchange survives restart (FR-027); on stream fault render `data-testid="chat-error"` (`role="alert"`) with the diagnosed cause, preserve partial text, return to ready (FR-035) (depends on T029, T030, T031, T009, T011).
- [ ] T033 [P] [US1] Add the code-copy JS + markdown/code-block styles in `src/FoundryStudio.App/wwwroot/` (Copper accent, WCAG AA, both Workshop Daylight & Night Forge): a clipboard helper invoked by `chat-code-copy` that copies the literal code passed from `RenderedMarkdown.CodeBlocks` (not an HTML-escaped variant), and styles for headings/lists/emphasis/inline + distinct fenced blocks.

**Checkpoint**: US1 independently DevFlow-verifiable — real incremental streaming, multi-turn, markdown + exact code-copy, no blocking (SC-001/002).

---

## Phase 7: User Story 2 — Gate chat on a loaded model, with load-on-demand (Priority: P1) — Hicks

**Goal**: When no model is loaded, present an honest gate + load-on-demand through the M1 `IModelStateGate`; when loaded, be immediately chat-ready and name the model; reflect unload-elsewhere and busy honestly.

**Independent Test (DevFlow DOM)**: With **no** model loaded, assert `chat-gate` shows and the composer does not accept a send; activate `chat-gate-load` → load runs via `LoadAsync` through the gate → surface becomes ready and `chat-active-model` names it; force contention → honest `chat-busy` (`ModelBusyException`), no hang.

- [ ] T034 [US2] Create `src/FoundryStudio.App/Components/Chat/ChatGate.razor` — `data-testid="chat-gate"` honest "a model must be loaded" (shown when `ListLoadedAsync` is empty, FR-007); `data-testid="chat-gate-load"` offering a cached model to load via `IFoundryCatalogService.LoadAsync(alias, …)` through the M1 gate (no bypass, FR-008); `data-testid="chat-active-model"` `aria-live="polite"` naming the loaded model when ready (FR-009). The composer/send is inert until ready — never accepts a send as though it would succeed (FR-007) (depends on T029).
- [ ] T035 [US2] Surface honest not-ready/busy states in `Chat.razor`: `data-testid="chat-busy"` (`role="alert"`) rendering a gate-rejected load (`ModelBusyException`) or a model unloaded elsewhere while Chat is open — re-check `ListLoadedAsync`, reflect not-ready, **no hang/corruption** (FR-009/010) (depends on T034).

**Checkpoint**: US1 + US2 = "load a model and chat"; both independently DevFlow-verifiable (SC-003).

---

## Phase 8: User Story 3 — Cancel / stop a streaming generation (Priority: P1) — Hicks

**Goal**: A Stop control (present **only** while streaming) cancels via the `CancellationToken`; streaming halts promptly, partial text is preserved as a stopped turn, the surface returns to ready, the next send works — no orphaned generation, no `.Result`/`.Wait()`.

**Independent Test (DevFlow DOM)**: Start a long generation; assert `chat-stop` is present only while streaming; activate it → streaming halts, partial text preserved (stop reason user-cancelled), surface ready, next send works; confirm the lease released (no orphan) and KI-005 clean.

- [ ] T036 [US3] Add the Stop control in `Composer.razor`/`Chat.razor`: `data-testid="chat-stop"` bound to the per-generation `CancellationTokenSource.Cancel()`, **rendered only while streaming** (FR-013); after cancel, finalize the turn via `TokenStatsAccumulator.Complete(StopReason.UserCancelled)` and persist the stopped turn with its real partial `Content` + `StopReason` (never a clean completion, FR-027) (depends on T032).
- [ ] T037 [US3] Ensure cancellation is clean & cooperative in `Chat.razor`/`ChatViewState`: the `await foreach` exits promptly on cancel, the generation lease is released (no orphaned background stream, FR-013), the surface returns to ready, history retains the stopped turn, and the next send proceeds normally (FR-012) — strictly no `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` (KI-005, FR-037) (depends on T036).

**Checkpoint**: Stop is prompt, non-destructive, lease-clean (SC-004).

---

## Phase 9: User Story 4 — Honest token metrics from the real stream (Priority: P1) — Hicks

**Goal**: A metrics row (mono, tertiary) showing TTFT, tokens/sec, total tokens, stop reason — each from the real stream, with honest "unknown" when a value is genuinely absent.

**Independent Test (DevFlow DOM)**: Assert `chat-metrics` shows `metric-ttft`/`metric-tps`/`metric-total`/`metric-stop` derived from the real stream (TTFT matches first-update arrival); when usage is absent, `metric-total` shows **"unknown"** (0 fabricated values).

- [ ] T038 [US4] Create `src/FoundryStudio.App/Components/Chat/MetricsRow.razor` — `data-testid="chat-metrics"` (mono, tertiary, DESIGN §8) binding the page's `GenerationMetrics` from `TokenStatsAccumulator.Complete(...)`: `data-testid="metric-ttft"` (send→first real update), `data-testid="metric-tps"` (observed tokens ÷ elapsed), `data-testid="metric-total"` (real usage **or** honest "unknown" when `TotalTokens == null`, FR-016), `data-testid="metric-stop"` (natural/max-tokens/user-cancelled/error/unknown, FR-015). Never a fabricated/guessed-exact value (FR-035) (depends on T032, T011).

**Checkpoint**: Metrics are real or honestly "unknown" (SC-005).

---

## Phase 10: User Story 5 — Per-chat system prompt + the FOUR real params (Priority: P2) — Hicks

**Goal**: A per-conversation system prompt and **only** the four FL-supported params (temperature, max-tokens, top_p, frequency_penalty) that flow into the request and persist.

**Independent Test (DevFlow DOM)**: Set `chat-system-prompt` → applied as the system message; adjust the four `param-*` controls → values flow into the request (cross-check the unit-tested mapping); assert `param-top-k`/`param-min-p`/`param-repeat-penalty`/`param-seed` are **absent** (FR-019).

- [ ] T039 [US5] Create `src/FoundryStudio.App/Components/Chat/SystemPromptEditor.razor` — `data-testid="chat-system-prompt"` + `<label>` bound to `ChatSession.SystemPrompt`, applied as the system message via `TranscriptAssembler` and persisted via `SaveAsync` (FR-017) (depends on T029, T009).
- [ ] T040 [US5] Create `src/FoundryStudio.App/Components/Chat/InferenceParamsPanel.razor` — **exactly four** controls, each `+ <label>`: `data-testid="param-temperature"` → `Temperature`, `data-testid="param-max-tokens"` → `MaxOutputTokens`, `data-testid="param-top-p"` → `TopP`, `data-testid="param-frequency-penalty"` → `FrequencyPenalty`, bound to `ChatSession.Parameters` and flowing into the request via `InferenceParameters.ToChatOptions` (FR-018). **No** control for `top_k`/`min_p`/`repeat_penalty`/`seed` anywhere — surfacing one would be a fake control (FR-019, SC-006). Params persist with the conversation (depends on T029, T003).

**Checkpoint**: System prompt applied; exactly four real params flow + persist; zero fake controls (SC-006).

---

## Phase 11: User Story 6 — Context-window tracker with honest estimate (Priority: P2) — Hicks

**Goal**: An explicitly-**estimated** context-usage indicator that advances, warns near the limit, and says "unknown" when the model's context length is unreported.

**Independent Test (DevFlow DOM)**: Grow the conversation; assert `chat-context` advances and is **labeled an estimate** in all states; near the limit assert `chat-context-warn`; on a model with unreported context length assert `chat-context-unknown` with **no** fabricated denominator.

- [ ] T041 [US6] Create `src/FoundryStudio.App/Components/Chat/ContextMeter.razor` — compute a chars≈token/4 usage estimate over the transcript, read `ModelInfo.ContextLength` via `IFoundryCatalogService.GetModelAsync(alias)`, call `ContextWindowEstimator.Estimate(used, contextLength)`, and render `data-testid="chat-context"` (advances; **labeled an estimate** in every state, in text not color, FR-020); `data-testid="chat-context-warn"` (`role="status"`, "older turns may fall out of context", FR-021); `data-testid="chat-context-unknown"` ("limit unknown" — **no** fabricated denominator/%, FR-022) (depends on T029, T014).

**Checkpoint**: Context tracker advances/warns; honest unknown; always an estimate (SC-007).

---

## Phase 12: User Story 7 — Persisted history + consent-gated destructive actions (Priority: P2) 🔒 Constitution IV load-bearing — Hicks

**Goal**: Conversations persist and reload on restart; new/duplicate are non-destructive; clear/delete only behind the reused M3 `ConfirmDialog` (names the conversation, "cannot be undone", Cancel default-focused); a single activation never deletes.

**Independent Test (DevFlow DOM)**: Hold a conversation, restart, assert it reloads from `chat-conversations` with turns + system prompt + params intact; `chat-new`/`chat-duplicate` create persisted conversations non-destructively; activating `chat-delete`/`chat-clear` opens a `confirm-dialog` that **names the conversation** + states it **cannot be undone**, `confirm-cancel` **default-focused**, **zero** removals on the single activation; `confirm-accept` removes via `DeleteAsync(userConfirmed:true)`.

- [ ] T042 [US7] Create `src/FoundryStudio.App/Components/Chat/ConversationList.razor` — `data-testid="chat-conversations"` from `IChatHistoryStore.ListAsync()` (reloads on restart, FR-023); `data-testid="chat-new"` (non-destructive create + `SaveAsync`, FR-024); `data-testid="chat-duplicate"` (non-destructive copy of the source — source unchanged — + `SaveAsync`, FR-024); `data-testid="chat-clear"` and `data-testid="chat-delete"` that **open** the reused `ConfirmDialog` and do NOT mutate on this single activation (FR-025) (depends on T029).
- [ ] T043 [US7] Wire the consent flow in `ConversationList.razor`/`Chat.razor` reusing `src/FoundryStudio.App/Components/Catalog/ConfirmDialog.razor` (M3): the dialog message **names the exact conversation** + states it **cannot be undone**; `confirm-accept` → `DeleteAsync(id, userConfirmed:true)` / `ClearMessagesAsync(id, true)` then refresh the list; `confirm-cancel` (`data-autofocus="true"`, **default-focused**) → no-op, conversation intact (FR-025/026). There is **no** one-click destructive path anywhere; the consent gate is the same enforcement point proven dylib-free in T021 (depends on T020, T042).
- [ ] T044 [US7] Ensure non-destructive persistence in `Chat.razor`/`ChatViewState`: every persisted change (new turn, params/system-prompt edit) bumps `UpdatedAt` and calls `SaveAsync` so an exchange the user saw is not silently lost across restart (FR-027); an interrupted/in-flight turn is persisted with its real partial content + stop reason, never as a clean completion (FR-027); a parse-failure on load preserves the file as `.bak` and surfaces honestly rather than wiping (Constitution IV) (depends on T020, T032, T036).

**Checkpoint**: History persists/reloads; new/duplicate non-destructive; clear/delete consent-gated, single-activation-safe (SC-008/009).

---

## Phase 13: User Story 8 — Tool / function calling with genuine .NET tools (Priority: P2) — Hicks (UI) / Bishop (wiring)

**Goal**: Real end-to-end function calling through `UseFunctionInvocation()` with one or two genuine tools; tool activity shown **only** when a real tool ran; tool errors surfaced plainly; plain chat unchanged when no tool is needed.

**Independent Test**: pure unit — `ToolInvocationWiringTests` (T022) proves invocation is genuine MEAI middleware; DevFlow DOM — on a tool-capable model, ask something warranting `get_current_time`/`get_loaded_model_info` and assert `chat-tool-activity` reflects a **real** invocation whose result is incorporated; a tool error surfaces as `chat-tool-error`; assert **no** decorative tool affordance exists.

- [ ] T045 [US8] Create `src/FoundryStudio.App/Components/Chat/` tool affordance (e.g. in `MessageBubble.razor` or a small `ToolActivity.razor`): `data-testid="chat-tool-activity"` shown **only** when a real tool executed (genuine invocation reflected from the response, FR-030); `data-testid="chat-tool-error"` (`role="alert"`) surfacing a tool error plainly, not hidden/faked (FR-029). When no tool is needed, plain streaming behaves exactly as US1 — **no** dead/decorative tool UI (depends on T029, T024).

**Checkpoint**: Genuine tool calling end-to-end; zero decorative tool UI (SC-010).

---

## Phase 14: User Story 9 — [OPTIONAL P3] Best-effort structured output / regenerate-last — does NOT gate M4

**Goal**: *(Optional stretch)* Best-effort `response_format` pass-through and/or regenerate-last. **M4's DoD does NOT depend on US9 (FR-033); it may be deferred without failing the milestone.**

- [ ] T046 [P] [US9] *(optional)* If structured output is surfaced at all, render `data-testid="chat-structured"` labeled **best-effort only** with **zero** "guaranteed/enforced JSON" claims and **no** enforcement-implying toggle (prefer omission, FR-031); `response_format` is merely passed through (already best-effort in T025). If regenerate-last is implemented, add `data-testid="chat-regenerate"` that re-streams a fresh reply for the **same** prior context and replaces the last assistant turn — simple regenerate-last only, no branching (FR-032). Otherwise omit entirely.

**Checkpoint**: If shipped, honestly best-effort with no enforcement claim; if deferred, DoD unaffected (FR-033, SC-011).

---

## Phase 15: Polish & Milestone Close (Cross-Cutting + Constitution II Verification)

**Purpose**: Prove M4 end-to-end on real Apple Silicon **without touching pre-existing user chat history**, record observed FL behavior, and write the `Verified:` line. DevFlow **DOM inspection** is the sanctioned autonomous evidence path (KI-001); light/dark **screenshots** are a frontmost-window supplement, never the gate.

- [ ] T047 Confirm the **dylib-free seam suite is green** (Quickstart Layer A, CI seam gate): `TranscriptAssemblerTests`, `InferenceParametersMappingTests`, `TokenStatsAccumulatorTests`, `ContextWindowEstimatorTests`, `ChatMarkdownTests`, `ChatHistoryDocumentTests`, `ChatHistoryConsentGateTests`, `ToolInvocationWiringTests`, the `NoBlockingInitGuard` test (KI-005), and all existing suites — `dotnet test tests/FoundryStudio.Tests/FoundryStudio.Tests.csproj` (SC-001/002/005/006/007/008/009/010).
- [ ] T048 **Consent-gate + confirm-flow proof** (data preservation, Constitution IV): via DevFlow DOM on Apple Silicon, assert `chat-delete`/`chat-clear` open a `confirm-dialog` that **names the conversation** with `confirm-cancel` default-focused and **zero** removals on the single activation; `confirm-cancel` removes nothing; **no** one-click destructive path. Combined with T021, this proves the gate dylib-free + the confirm-flow DOM (SC-009, FR-025/026).
- [ ] T049 **DevFlow DOM e2e** (Quickstart Layer B, steps 1–11) on real Apple Silicon: nav-chat activation → gate + load-on-demand (+ honest busy) → streaming multi-turn (assert `chat-msg-assistant` shows **multiple** partial renders `data-streaming` true→false, never a single paste/typewriter) → markdown + exact code-copy → awaiting-state ("Ask this local model…", Copper send) → Stop (partial preserved, lease clean, KI-005) → honest metrics (TTFT real; `metric-total` "unknown" when usage absent) → system prompt + the four params (and `param-top-k`/`min-p`/`repeat-penalty`/`seed` **absent**) → context estimate/warn/unknown → **persist-reload after restart** → consent-gated delete. **The single live delete targets ONLY the conversation the test itself created — pre-existing user chat history is NEVER touched (FR-040).**
- [ ] T050 [P] **Negative / honesty-invariant DOM sweep** (SC-005/006/007/011): assert **exactly four** param controls and **zero** for `top_k`/`min_p`/`repeat_penalty`/`seed`; **zero** "guaranteed/enforced JSON" claim or enforcement toggle; **zero** fabricated tokens/metrics (a metric renders "unknown" when absent); context tracker renders "unknown" with no fabricated denominator when `ContextLength` is null; `chat-tool-activity` appears **only** when a real tool executed (no decorative tool UI).
- [ ] T051 [P] **Accessibility AA verification + light/dark screenshots** (FR-036, SC-012): via DevFlow computed-style/DOM in **both** Workshop Daylight and Night Forge, confirm every new control (composer/send, Stop, system-prompt + the four params, context tracker, conversation new/duplicate/clear/delete + confirmation, code-copy, tool affordances) is labeled, keyboard-reachable, announces state; the streamed region is `aria-live="polite"`; the confirm dialog is `role="dialog" aria-modal="true"`, focus-trapped, Esc=cancel, Cancel default-focused; states are text+icon (never color-alone); the "estimate" qualifier is text; contrast meets AA. Capture frontmost-window light + dark screenshots as supplementary evidence.
- [ ] T052 [US8] **Tool round-trip on hardware** (SC-010): on a tool-capable model, ask something warranting `get_current_time`/`get_loaded_model_info`; assert `chat-tool-activity` reflects a **real** invocation incorporated into the reply and a forced tool error surfaces as `chat-tool-error`. Record observed FL tool-honoring behavior (R5).
- [ ] T053 Update `KNOWN-ISSUES.md`: record observed FL/Betalgo **usage + finish-reason** emission (R2 — whether `UsageContent`/`ChatFinishReason` populate; honest "unknown" is the passing fallback), FL **cancel-honoring** on Stop, and **tool round-trip** behavior; note the KI-001 DOM-evidence path used and that KI-005 stayed clean in the chat path.
- [ ] T054 **Independent review** (Constitution II/III, FR-039): a reviewer who is **not** the author runs `/review` over the M4 change set (Core seams + Foundry wiring + Blazor UI), with explicit attention to the data-preservation flow (consent gate, confirm-flow, `.bak` recovery, persist-reload) and the honesty invariants (real stream only, four params only, no enforcement claims). Author does not self-approve.
- [ ] T055 Write the M4 milestone-close note with a **`Verified:`** line naming the checks that ran (the eight dylib-free seam suites + `NoBlockingInitGuard` + existing; the DevFlow DOM e2e: nav-chat/gate+load-on-demand/streaming-multi-turn(partial renders real)/markdown+code-copy/stop(partial preserved)/metrics(TTFT real, total=&lt;obs&gt;)/4-params-only/context-estimate/persist-reload-after-restart/consent-delete-on-test-convo/tool-call(&lt;obs&gt;); user history untouched; FL usage=&lt;obs&gt;, finish-reason=&lt;obs&gt;, cancel-honored=&lt;obs&gt;, tools-honored=&lt;obs&gt;), and record the GO/NO-GO milestone decision (depends on T047–T054).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies; T002 (Markdig ref) before any seam that uses Markdig (T016).
- **Foundational (Phase 2)**: after Setup — **BLOCKS everything**. T003 before T007; T004 before T006; T006 before T007; T007 before T008/T009/T018.
- **Core seams (Phase 3)**: after Foundational — the coordinator-authored dylib-free backbone. **BLOCKS** the Foundry wiring and the UI that consume each seam.
- **Foundry wiring (Phase 4)**: after the seams it threads (T003 mapping, T011 metrics shape). T024→T025→T026; T023 independent.
- **UI scaffold (Phase 5)**: after Foundational + the seams/services it registers (T020, T023). **BLOCKS** all UI stories.
- **UI stories (Phases 6–14)**: after UI scaffold; each consumes specific seams (US1↔T009/T016, US4↔T011, US5↔T003, US6↔T014, US7↔T020, US8↔T023/T024).
- **Polish/close (Phase 15)**: after the in-scope stories complete (T049 needs US1–US8; US9 optional).

### Within each story

- Pure-seam tests (T010/T012/T013/T015/T017/T019/T021/T022) are written to **FAIL first**, then their seam makes them green.
- Core seams before the UI that consumes them; Foundry-layer wiring before the UI calls that depend on it.
- Story complete and DevFlow-verifiable before moving to the next priority.

### Shared-file coordination (NOT parallel within a story)

- `src/FoundryStudio.App/Components/Pages/Chat.razor` + `ChatViewState` are edited by T029 (scaffold), T032/T033 (US1), T035 (US2), T036/T037 (US3), T043/T044 (US7) — **serialize these edits**.
- `src/FoundryStudio.App/Components/Chat/Composer.razor` is edited by T030 (US1) and T036 (US3 Stop) — serialize.
- `src/FoundryStudio.Foundry/FoundryChatClient.cs` / `Internal/FoundryMessageMapper.cs` are edited by T024/T025/T026 — serialize.
- `src/FoundryStudio.App/MauiProgram.cs` is edited by T028 only.

### Parallel opportunities

- Foundational: T003 ∥ T004 ∥ T005 ∥ T008 (distinct new files); then T006, T007.
- Core seams (Phase 3): the test+impl pairs are `[P]` across seams — T009/T010, T011/T012, T013, T014/T015, T016/T017, T018/T019, T022 touch distinct new files and can be authored together (T020/T021 follow T018).
- Foundry wiring: T023 ∥ the T024→T025→T026 chain.
- UI stories after scaffold: US4 (T038), US5 (T039/T040), US6 (T041) are largely independent `[P]` across distinct new components; US1/US3 share `Composer`/`Chat.razor` (serialize).
- Polish: T050 ∥ T051 (different evidence sweeps).

---

## Parallel Example: Core dylib-free seam tests + impls (after Foundational)

```bash
# Write the failing dylib-free seam tests together (different files):
Task: "TranscriptAssemblerTests in tests/FoundryStudio.Tests/TranscriptAssemblerTests.cs"
Task: "TokenStatsAccumulatorTests in tests/FoundryStudio.Tests/TokenStatsAccumulatorTests.cs"
Task: "InferenceParametersMappingTests in tests/FoundryStudio.Tests/InferenceParametersMappingTests.cs"
Task: "ContextWindowEstimatorTests in tests/FoundryStudio.Tests/ContextWindowEstimatorTests.cs"
Task: "ChatMarkdownTests in tests/FoundryStudio.Tests/ChatMarkdownTests.cs"
Task: "ChatHistoryDocumentTests in tests/FoundryStudio.Tests/ChatHistoryDocumentTests.cs"
Task: "ToolInvocationWiringTests in tests/FoundryStudio.Tests/ToolInvocationWiringTests.cs"

# Then implement the pure seams in parallel (different new files):
Task: "TranscriptAssembler.cs in src/FoundryStudio.Core/Chat/"
Task: "TokenStatsAccumulator.cs in src/FoundryStudio.Core/Chat/"
Task: "ContextWindowEstimator.cs in src/FoundryStudio.Core/Chat/"
Task: "ChatMarkdown.cs in src/FoundryStudio.Core/Chat/"
Task: "ChatHistoryDocument.cs + FileChatHistoryStore.cs in src/FoundryStudio.Core/Chat/"
```

---

## Implementation Strategy

### MVP first (US1 + US2 + US3 = the P1 streaming chat)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 Core seams (coordinator, dylib-free, all green) → 4. Phase 4 Foundry wiring → 5. Phase 5 UI scaffold → 6. US1 (streaming + markdown + code-copy) → 7. US2 (gate + load-on-demand) → 8. US3 (stop/cancel) → **STOP & VALIDATE** via DevFlow DOM. This is the smallest shippable, honesty-correct, KI-005-clean streaming chat.

### Incremental delivery

Core seams → Foundry wiring → UI scaffold → US1 → US2 → US3 → US4 (metrics) → US5 (system prompt + 4 params) → US6 (context estimate) → US7 (persist + consent) → US8 (tools) → US9 (optional) → Polish/close. Each story is independently DevFlow-verifiable and adds value without breaking the prior ones.

---

## Suggested Squad Ownership (consistent with prior milestones)

| Owner | Scope in M4 | Primary tasks |
|-------|-------------|---------------|
| **Ripley** (lead / coordinator) | Core dylib-free seams + their unit tests; milestone-close arbitration; `Verified:` sign-off + GO/NO-GO | T002–T022 (Core seams + tests), T055 |
| **Bishop** (FL wiring) | MEAI pipeline build, options/usage/tools mapping, genuine example tools; FL usage/finish-reason on hardware | T023–T026, T052 |
| **Hicks** (Blazor chat UI) | nav activation, Chat page + view-state, composer/message-list/markdown+code-copy, gate, stop, metrics, params panel, context meter, conversation list + consent reuse, tool UI, AA styling | T027–T046 (UI), T051 |
| **Drake** (tests / CI) | Dylib-free seam suite green; DevFlow DOM e2e; negative-invariant + AA sweeps; light/dark screenshots | T001, T047–T050 |
| **Spunkmeyer** (PR quality + data-preservation review) | Independent review (reviewer ≠ author); the data-preservation + honesty flow review; KNOWN-ISSUES hygiene | T053, T054 |
| **(shared)** | DevFlow e2e on Apple Silicon + frontmost-window light/dark screenshot supplement (KI-001) | T049, T051 |

> Reviewer independence (FR-039): the author of a change set must not be its sole approver — Spunkmeyer/Ripley review what Hicks/Bishop author, and vice-versa. The data-preservation flow (T048 consent gate + T049 persist-reload + consent-delete-on-test-convo) gets an explicit independent review pass.

---

## Notes

- `[P]` = different files, no dependency on an incomplete task.
- `[Story]` labels (US1–US8) trace each task to its spec user story; Setup/Foundational/Foundry-wiring/UI-scaffold/Polish carry none.
- Pure-seam tests (T010/T012/T013/T015/T017/T019/T021/T022) must **FAIL before** their implementation lands.
- Every task preserves the M4 guardrails at the top: **capability honesty** (real stream only, honest unknown/estimate/unsupported, four params only, no enforcement claims, no dead tool UI), **data preservation** (persist-survives-restart, consent-gated clear/delete, single-activation-safe, `.bak` recovery, live-delete-only-on-test-convo FR-040), **layering** (App → `IChatService`/`IChatHistoryStore`/M1–M3 services/Core seams only, never FL SDK), **in-process** (no loopback) + **gate-leased** streaming, **KI-005** (no `.Result`/`.Wait()`), dylib-free seams, WCAG AA in both themes.
- Commit after each task or logical group; stop at any checkpoint to validate the story independently via DevFlow DOM (KI-001 — DOM is the autonomous evidence path; screenshots need the window frontmost).
- US9 is optional/P3 — its absence does **not** block M4's DoD (FR-033).
