# Contract: Core pure-logic seams (new in M4)

All seams below live in `FoundryForge.Core`, are **FL-free and dylib-free**, and are unit-tested in `tests/FoundryForge.Tests` without a native Foundry Local dylib (FR-038, SC-001/002/005/006/007/008/009). They mirror the M1–M3 precedent (`SettingsDocument`, `RamFitHeuristic`, `ConsentGuard`). `FoundryForge.Core` references `Microsoft.Extensions.AI` (already) and adds a `PackageReference` to the already-pinned `Markdig 0.44.0` (refresh `packages.lock.json` — research R1).

---

## `TranscriptAssembler` — session → request messages (US1)

`src/FoundryForge.Core/Chat/TranscriptAssembler.cs`

```csharp
public static IReadOnlyList<ChatMessage> Assemble(ChatSession session);
```

**Contract**
- When `session.SystemPrompt` is non-empty ⇒ first message is `new ChatMessage(ChatRole.System, SystemPrompt)` (FR-017); empty/null ⇒ no system message.
- Then each `ChatMessageRecord` maps **in order** to a MEAI `ChatMessage` (`ChatTurnRole`→`ChatRole`) — full multi-turn context (FR-004).
- Tool turns map to `ChatRole.Tool`; never drops or reorders turns.
- Pure, deterministic, dylib-free; `null`/empty session messages ⇒ system message only (or empty list).

**Test fixtures** (`TranscriptAssemblerTests`, SC-001): system + 2 user/assistant pairs → correct order & roles; no system prompt → no system message; tool turn preserved.

---

## `InferenceParameters` — the four real params → `ChatOptions` (US5, R6)

`src/FoundryForge.Core/Chat/InferenceParameters.cs`

```csharp
public sealed record InferenceParameters(double? Temperature = null, int? MaxOutputTokens = null,
    double? TopP = null, double? FrequencyPenalty = null)
{
    public static InferenceParameters Defaults { get; }
    public ChatOptions ToChatOptions(string modelId, IEnumerable<AITool>? tools = null);
}
```

**Contract**
- `ToChatOptions` sets **exactly** `Temperature`, `MaxOutputTokens`, `TopP`, `FrequencyPenalty`, `ModelId`, and `Tools` — and **nothing** for `top_k`/`min_p`/`repeat_penalty`/`seed` (no such property exists; FR-019).
- A `null` param leaves the corresponding `ChatOptions` property unset (engine default) — no fabricated value.
- `tools` (when provided) flows to `ChatOptions.Tools` for `UseFunctionInvocation()`.
- Pure; no FL, no I/O.

**Test fixtures** (`InferenceParametersMappingTests`, SC-006): each of the four flows into the matching `ChatOptions` property; `ChatOptions` exposes **no** unsupported-param surface (assert the four are set, nulls unset); tools attach.

---

## `TokenStatsAccumulator` + `GenerationMetrics`/`StopReason` — honest metrics (US4, R2)

`src/FoundryForge.Core/Chat/TokenStatsAccumulator.cs`, `src/FoundryForge.Core/Models/GenerationMetrics.cs`

```csharp
public sealed class TokenStatsAccumulator
{
    public void OnSend(DateTimeOffset sentAt);
    public void OnUpdate(ChatResponseUpdate update, DateTimeOffset arrivedAt);
    public GenerationMetrics Complete(StopReason stopReason);
}
public sealed record GenerationMetrics(TimeSpan? TimeToFirstToken, double? TokensPerSecond,
    int? TotalTokens, StopReason StopReason);
public enum StopReason { Natural, MaxTokens, ToolCalls, UserCancelled, Error, Unknown }
```

**Contract**
- **TTFT** = first text update's `arrivedAt` − `sentAt` (always real; null only if no update ever arrived).
- **TokensPerSecond** = observed token/segment count ÷ elapsed (real timing/counts); null if not derivable.
- **TotalTokens** = `UsageContent.Details.TotalTokenCount` when a `UsageContent` was observed; otherwise **`null` = honest "unknown"** (never back-computed-and-presented-as-exact, FR-016).
- **StopReason** = the argument to `Complete` (UI passes `UserCancelled` on Stop, `Error` on fault, or the mapped `ChatFinishReason`); `Unknown` when absent (FR-015).
- Clock-injected (timestamps passed in) ⇒ deterministic; pure aside from internal accumulation; no real-clock/IO.

**Test fixtures** (`TokenStatsAccumulatorTests`, SC-005): synthetic updates with timestamps → TTFT & tok/sec computed; a trailing `UsageContent` → real total; **no** usage → `TotalTokens == null`; `Complete(UserCancelled)` → stop reason honored.

---

## `ContextWindowEstimator` + `ContextWindowEstimate` — honest estimate (US6, R7)

`src/FoundryForge.Core/Chat/ContextWindowEstimator.cs`, `src/FoundryForge.Core/Models/ContextWindowEstimate.cs`

```csharp
public static ContextWindowEstimate Estimate(int usedTokensEstimate, int? contextLength, double warnFraction = 0.8);
public sealed record ContextWindowEstimate(int UsedTokensEstimate, int? ContextLength,
    double? Fraction, bool IsWarn, bool IsUnknown);
```

**Contract**
- `contextLength is null` ⇒ `IsUnknown = true`, `Fraction = null`, `IsWarn = false` — UI says "limit unknown", **no fabricated denominator/percentage** (FR-022).
- known `contextLength` ⇒ `Fraction = usedTokensEstimate / contextLength`; `IsWarn = Fraction >= warnFraction` (FR-021).
- The figure is always presented as an **estimate** in the UI (FR-020) — the seam name and `…Estimate` types encode that.
- The token approximation (chars≈token/4 over the transcript) is the caller's input; documented as an estimate.
- Pure, deterministic; no I/O.

**Test fixtures** (`ContextWindowEstimatorTests`, SC-007): usage advances fraction; crossing `warnFraction` ⇒ `IsWarn`; null context ⇒ `IsUnknown`, null fraction; zero usage ⇒ fraction 0.

---

## `ChatMarkdown` + `RenderedMarkdown`/`CodeBlock` — safe markdown + code copy (US1, R1)

`src/FoundryForge.Core/Chat/ChatMarkdown.cs`

```csharp
public static RenderedMarkdown Render(string markdown);
public readonly record struct CodeBlock(string Language, string Code);
public sealed record RenderedMarkdown(string Html, IReadOnlyList<CodeBlock> CodeBlocks);
```

**Contract**
- Renders via a Markdig pipeline built with **`.DisableHtml()`** so raw HTML in model output is encoded, not executed (HTML-injection defense, FR-005).
- `CodeBlocks` lists each fenced block's **exact raw code** + language (from the parsed AST) so the UI Copy control copies the literal code, not an HTML-escaped/re-serialized variant (US1.4).
- Renders headings/lists/emphasis/inline code/fenced blocks correctly (FR-005); deterministic for a given input.
- Pure; no I/O; dylib-free (Markdig is managed).

**Test fixtures** (`ChatMarkdownTests`, SC-002): headings/lists/emphasis render; a fenced `csharp` block appears in `CodeBlocks` with exact code + language `csharp`; a `<script>`/raw-HTML input is **encoded** in `Html` (not active markup).

---

## `IChatHistoryStore` + `FileChatHistoryStore` + `ChatHistoryDocument` — persistence & consent (US7, R3)

`src/FoundryForge.Core/Abstractions/IChatHistoryStore.cs`, `src/FoundryForge.Core/Chat/FileChatHistoryStore.cs`, `…/Chat/ChatHistoryDocument.cs`

```csharp
public interface IChatHistoryStore
{
    Task<IReadOnlyList<ChatSession>> ListAsync(CancellationToken ct = default);
    Task<ChatSession?> GetAsync(string id, CancellationToken ct = default);
    Task SaveAsync(ChatSession session, CancellationToken ct = default);
    Task DeleteAsync(string id, bool userConfirmed, CancellationToken ct = default);
    Task ClearMessagesAsync(string id, bool userConfirmed, CancellationToken ct = default);
}
```

**Contract (Constitution IV — load-bearing)**
- **Consent gate**: `DeleteAsync`/`ClearMessagesAsync` with `userConfirmed:false` **remove nothing** — guard before any file mutation (mirrors `FileSettingsService.ResetAsync` / `DeleteFromCacheAsync`). This is the dylib-free enforcement point (SC-009, FR-026).
- **Persistence**: `SaveAsync` upserts a full `ChatSession` (turns + system prompt + params) as human-readable JSON (one `<id>.json` per conversation) so it **survives restart** (FR-023/027). `ListAsync`/`GetAsync` reload from disk.
- **Round-trip**: `ChatHistoryDocument.Serialize`/`Deserialize` (mirrors `SettingsDocument`) preserves all fields incl. `SchemaVersion` (SC-008).
- **Non-destructive recovery**: an unparseable file is preserved as `.bak`; never silently overwritten/wiped (Constitution IV).
- **Interrupted turn**: a stopped/errored assistant turn persists with its real partial content + stop reason — never as a clean completion (FR-027).
- The App injects the `chats` directory path (keeps MAUI/`FileSystem` out of Core). `FileChatHistoryStore` is plain managed file IO ⇒ unit-testable with a temp dir.

**Test fixtures**:
- `ChatHistoryDocumentTests` (SC-008): a `ChatSession` with system prompt + params + multi-turn messages round-trips field-for-field; garbage JSON ⇒ recoverable (no throw out of read).
- `ChatHistoryConsentGateTests` (SC-009): `DeleteAsync(id, userConfirmed:false)` and `ClearMessagesAsync(id, false)` against a temp store **remove nothing** (file still present, messages intact); `userConfirmed:true` removes. Proven without any FL/dylib.

---

## `ChatTools` (Foundry layer — referenced here for completeness) — genuine tools (US8, R5)

`src/FoundryForge.Foundry/Tools/ChatTools.cs` — **not** a Core seam (it touches `IFoundryCatalogService`), but its *wiring* is unit-testable in Core via a fake `IChatClient` through `UseFunctionInvocation()`:
- `get_current_time` → current local ISO 8601 datetime (real, no network).
- `get_loaded_model_info` → active model alias + context length via `ListLoadedAsync()` (real app state).

**Test fixture** (`ToolInvocationWiringTests`, SC-010): a fake `IChatClient` scripted to emit a tool call, wrapped with `AsBuilder().UseFunctionInvocation().Build()`, invokes a **real** test `AIFunction` and feeds its result back — proving invocation is MEAI middleware, not faked (FR-029). Dylib-free.

---

## Notes
- These seams contain **no fabrication**: every "unknown"/"estimate" state is honest and derived from inputs (Constitution III/IV).
- All seams are independently unit-testable with zero native dependencies, satisfying FR-038 and keeping the CI seam gate green.
