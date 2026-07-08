# Phase 1 Data Model: M4 — Chat experience (v1 core)

M4 introduces a **new persistent store** (chat history — user data under Constitution IV) plus several **pure-logic Core seams** (dylib-free, unit-tested — `FoundryForge.Core`) and transient UI view-model state (`FoundryForge.App`). Existing entities (`ModelInfo`, MEAI `ChatMessage`/`ChatOptions`/`ChatResponseUpdate`/`ChatRole`/`ChatFinishReason`) are consumed unchanged. All persisted/derived entities are FL-free and serialize/round-trip without a native dylib (FR-038).

---

## Existing entities (consumed, unchanged)

### `ModelInfo` — `src/FoundryForge.Core/Models/ModelInfo.cs`
Consumed in M4: `Alias` (active model identity), **`ContextLength` (`int?`)** (context-window estimate denominator; null ⇒ honest unknown, R7), `IsLoaded`. No schema change.

### MEAI types (Microsoft.Extensions.AI 10.0.1)
- **`ChatMessage`** — assembled per request by `TranscriptAssembler`; the system message carries the per-chat system prompt.
- **`ChatOptions`** — produced by `InferenceParameters.ToChatOptions(...)`; carries `Temperature`/`MaxOutputTokens`/`TopP`/`FrequencyPenalty`/`ModelId`/`Tools`. **No** unsupported-param surface.
- **`ChatResponseUpdate`** — the only honest source of assistant text, `Contents` (incl. `UsageContent`), and `FinishReason`. Never fabricated.
- **`ChatFinishReason`** — mapped to `StopReason` (Stop/Length/ToolCalls); user Stop overrides to user-cancelled.

---

## New persistent entities (user data — Constitution IV)

### `ChatSession` — `src/FoundryForge.Core/Models/ChatSession.cs`
The persisted, multi-turn conversation (one JSON file under `<AppData>/chats/<id>.json`, R3).
```csharp
public sealed record ChatSession(
    string Id,                              // stable guid
    string Title,                           // user-visible name (default from first user message)
    string? SystemPrompt,                   // per-chat system prompt (US5); null ⇒ no system message
    InferenceParameters Parameters,         // the four real params (persisted)
    string? ModelAlias,                     // model last used with this conversation
    IReadOnlyList<ChatMessageRecord> Messages,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int SchemaVersion = 1);
```
| Field | Rule |
|-------|------|
| `Id` | stable guid; the file name; the delete/clear target |
| `Title` | non-destructive default derived from first user message; user-editable |
| `SystemPrompt` | persisted; applied as the system message via `TranscriptAssembler` (FR-017) |
| `Parameters` | persisted with the conversation; survives restart (FR-023) |
| `Messages` | ordered turns; an interrupted/stopped turn stores its real partial state, never a clean completion (FR-027) |
| `UpdatedAt` | bumped on every persisted change (new turn, params/system-prompt edit) |

**Survives restart** via `ChatHistoryDocument` round-trip (SC-008). **Destructive ops** (delete/clear) only through the consent gate (FR-025/026).

### `ChatMessageRecord` — `src/FoundryForge.Core/Models/ChatMessageRecord.cs`
A single persisted turn (FL-free; mapped to MEAI `ChatMessage` at request time — never persist MEAI types directly, R3).
```csharp
public sealed record ChatMessageRecord(
    ChatTurnRole Role,                      // System | User | Assistant | Tool
    string Content,
    DateTimeOffset CreatedAt,
    GenerationMetrics? Metrics = null,      // assistant turns only; null for user/system
    StopReason? StopReason = null);         // assistant turns only

public enum ChatTurnRole { System, User, Assistant, Tool }
```
- Maps to/from MEAI `ChatRole` in `TranscriptAssembler` (Core, dylib-free).
- A **stopped/interrupted** assistant turn stores its partial `Content` + `StopReason = UserCancelled`/`Error` (FR-027) — honest, never "complete".

---

## New Core seam entities (pure, dylib-free)

### `InferenceParameters` — `src/FoundryForge.Core/Chat/InferenceParameters.cs`
The **only four** FL-supported params (US5). Excludes `top_k`/`min_p`/`repeat_penalty`/`seed` **by construction**.
```csharp
public sealed record InferenceParameters(
    double? Temperature = null,
    int? MaxOutputTokens = null,
    double? TopP = null,
    double? FrequencyPenalty = null)
{
    public static InferenceParameters Defaults { get; }      // sensible, honest defaults
    public ChatOptions ToChatOptions(string modelId, IEnumerable<AITool>? tools = null);
}
```
**Contract**: `ToChatOptions` sets exactly `Temperature`/`MaxOutputTokens`/`TopP`/`FrequencyPenalty`/`ModelId`/`Tools` and **nothing** for unsupported params (FR-019). Null fields leave the corresponding `ChatOptions` property unset (engine default). Pure; unit-tested (SC-006).

### `GenerationMetrics` — `src/FoundryForge.Core/Models/GenerationMetrics.cs`
Derived-from-stream display values; **each nullable = honest "unknown"** (FR-016).
```csharp
public sealed record GenerationMetrics(
    TimeSpan? TimeToFirstToken,   // measured: first-update arrival − send
    double? TokensPerSecond,      // observed tokens ÷ elapsed
    int? TotalTokens,             // UsageContent.TotalTokenCount, else null = "unknown"
    StopReason StopReason);       // see below
```
**Producer**: `TokenStatsAccumulator`. Display-only; an "unknown" metric is never persisted as if authoritative.

### `StopReason` — `src/FoundryForge.Core/Models/GenerationMetrics.cs`
```csharp
public enum StopReason { Natural, MaxTokens, ToolCalls, UserCancelled, Error, Unknown }
```
Mapped from MEAI `ChatFinishReason` (`Stop`→Natural, `Length`→MaxTokens, `ToolCalls`→ToolCalls); user Stop (US3) ⇒ `UserCancelled`; stream fault ⇒ `Error`; absent ⇒ `Unknown` (FR-015).

### `TokenStatsAccumulator` — `src/FoundryForge.Core/Chat/TokenStatsAccumulator.cs`
Deterministic, clock-injected accumulator (R2).
| Method | Effect |
|--------|--------|
| `OnSend(DateTimeOffset t0)` | marks send time |
| `OnUpdate(ChatResponseUpdate u, DateTimeOffset tNow)` | first text update sets TTFT; accumulate token/char count + last timestamp; capture `UsageContent` if present |
| `Complete(StopReason reason)` → `GenerationMetrics` | finalize TTFT, tok/sec, total (usage or "unknown"), stop reason |

Pure (no real clock inside) ⇒ unit-testable over a synthetic update sequence (SC-005). State transitions: `Idle → Streaming(first update) → Completed`.

### `ContextWindowEstimate` — `src/FoundryForge.Core/Models/ContextWindowEstimate.cs`
```csharp
public sealed record ContextWindowEstimate(
    int UsedTokensEstimate,
    int? ContextLength,           // null ⇒ unknown
    double? Fraction,             // null when ContextLength null — no fabricated denominator
    bool IsWarn,
    bool IsUnknown);
```
**Producer**: `ContextWindowEstimator.Estimate(int usedTokensEstimate, int? contextLength, double warnFraction = 0.8)` (R7). Always **labeled an estimate** in the UI; `IsUnknown` ⇒ honest "limit unknown", no `Fraction` (FR-022). Pure; unit-tested (SC-007).

### `TranscriptAssembler` — `src/FoundryForge.Core/Chat/TranscriptAssembler.cs`
```csharp
public static IReadOnlyList<ChatMessage> Assemble(ChatSession session);
```
**Contract**: prepends the system message when `SystemPrompt` is non-empty, then maps each `ChatMessageRecord` to a MEAI `ChatMessage` in order (multi-turn context, FR-004). Pure; dylib-free; unit-tested (SC-001).

### `ChatMarkdown` — `src/FoundryForge.Core/Chat/ChatMarkdown.cs`
```csharp
public static RenderedMarkdown Render(string markdown);   // Markdig pipeline w/ .DisableHtml()
public readonly record struct CodeBlock(string Language, string Code);
public sealed record RenderedMarkdown(string Html, IReadOnlyList<CodeBlock> CodeBlocks);
```
**Contract**: renders sanitized HTML (`DisableHtml()` ⇒ raw model HTML encoded, FR-005) and extracts each fenced block's **exact raw code + language** for the UI Copy control (US1.4). Pure; unit-tested incl. an HTML-injection case (SC-002).

---

## New persistence abstraction + impl

### `IChatHistoryStore` — `src/FoundryForge.Core/Abstractions/IChatHistoryStore.cs`
```csharp
public interface IChatHistoryStore
{
    Task<IReadOnlyList<ChatSession>> ListAsync(CancellationToken ct = default);
    Task<ChatSession?> GetAsync(string id, CancellationToken ct = default);
    Task SaveAsync(ChatSession session, CancellationToken ct = default);      // upsert; survives restart
    Task DeleteAsync(string id, bool userConfirmed, CancellationToken ct = default);  // consent-gated
    Task ClearMessagesAsync(string id, bool userConfirmed, CancellationToken ct = default); // consent-gated
}
```
**Contract (Constitution IV)**:
- `DeleteAsync`/`ClearMessagesAsync` with **`userConfirmed:false` remove nothing** (no-op/throw before any file mutation) — the dylib-free enforcement point (SC-009).
- `SaveAsync` is an upsert that persists the full `ChatSession` so the exchange survives restart (FR-023/027).

### `FileChatHistoryStore` — `src/FoundryForge.Core/Chat/FileChatHistoryStore.cs`
File impl (mirrors `FileSettingsService`; the App injects the `chats` directory path). One `<id>.json` per conversation via `ChatHistoryDocument`; `SemaphoreSlim` IO guard; write-temp-then-replace; **non-destructive recovery** — an unparseable file is preserved as `.bak`, never silently overwritten (Constitution IV). Plain managed file IO ⇒ directly unit-testable with a temp dir.

### `ChatHistoryDocument` — `src/FoundryForge.Core/Chat/ChatHistoryDocument.cs`
Mirrors `SettingsDocument`: `Serialize(ChatSession)` / `Deserialize(json)` with `System.Text.Json` (indented, stable names, `SchemaVersion`). Round-trip is the unit-tested seam (SC-008). Human-readable / user-auditable (Constitution IV).

---

## Foundry-layer mapping (FL-bound, verified on hardware)

| Concern | Where | Note |
|---------|-------|------|
| `ChatOptions` (4 params + tools + best-effort `response_format`) → FL request | `FoundryMessageMapper` (CHANGED) | the single Betalgo touchpoint (KI-007 #3) |
| Stream usage / finish-reason → `ChatResponseUpdate` (`UsageContent` + `FinishReason`) | `FoundryChatClient` (CHANGED) | emit only when FL provides it; else absent ⇒ "unknown" (R2) |
| App-defined tools (`AIFunction`) | `Foundry/Tools/ChatTools.cs` (NEW) | `get_current_time`, `get_loaded_model_info` (R5) |

---

## Transient UI view-model state (`FoundryForge.App`, not persisted)

| State | Component | Purpose |
|-------|-----------|---------|
| `ChatSurfaceState` | `Chat.razor` | active session, ready/not-ready/gate, streaming flag, current `CancellationTokenSource`, busy/error message |
| live assistant buffer | `MessageList`/`MessageBubble` | accumulating partial text re-rendered per real update (off-dispatcher) |
| live `TokenStatsAccumulator` | `Chat.razor` | feeds `MetricsRow` during/after a turn |
| consent dialog state | reused `ConfirmDialog.razor` | clear/delete confirmation (Cancel default-focus) |

These are transient; only `ChatSession` (via `IChatHistoryStore`) is persisted.

---

## Honesty & data-preservation invariants (cross-entity)
- Every assistant token and every metric originates from a real `ChatResponseUpdate`; genuinely-absent metrics are `null` ⇒ honest "unknown" (FR-016/035), never fabricated.
- `InferenceParameters` has **no field** for `top_k`/`min_p`/`repeat_penalty`/`seed` — unsupported params are impossible by construction (FR-019).
- `ContextWindowEstimate` never invents a denominator/percentage when `ContextLength` is null (FR-022).
- `ChatSession` is user data: persisted, restart-surviving, and only deleted/cleared through the `userConfirmed` consent gate (FR-025/026, SC-009).
