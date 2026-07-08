# Contract: Service surface relied on by M4 (+ the additive deltas)

M4 is overwhelmingly UI + pure seams. It relies on the **existing** `IChatService`, `IFoundryCatalogService`, and `IModelStateGate` surface, and introduces: (a) the new `IChatHistoryStore` abstraction (see `core-seams.md`); (b) the MEAI pipeline build at the documented `ChatService` seam; and (c) bounded options/usage/tools wiring inside the Foundry layer. The UI consumes **only** these abstractions + Core seams — never the FL SDK (FR-034, Constitution V / DEC-004).

---

## `IChatService` (`src/FoundryForge.Core/Abstractions/IChatService.cs`) — unchanged

```csharp
IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
    IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default);
```

| Aspect | M4 use | Story/FR |
|--------|--------|----------|
| `messages` | the assembled transcript from `TranscriptAssembler.Assemble(session)` (system + multi-turn) | US1 / FR-004 |
| `options` | `InferenceParameters.ToChatOptions(modelId, tools)` — the 4 real params + `ModelId` + `Tools` | US5/US8 / FR-018/028 |
| `cancellationToken` | the Stop control's per-generation `CancellationTokenSource.Token` | US3 / FR-011 |
| return | consumed with `await foreach` off the dispatcher; each update drives incremental render + `TokenStatsAccumulator` | US1/US4 / FR-003/014 |

**No signature change.** The seam already exists; M4 builds the surface that consumes it. In-process only — **no loopback** (FR-002).

---

## `ChatService` pipeline build (`src/FoundryForge.Foundry/ChatService.cs`) — CHANGED

The constructor builds the MEAI middleware pipeline at the already-documented seam (research R4):
```csharp
// BEFORE: _chatClient = adapter;
// AFTER:
_chatClient = adapter.AsBuilder()
    .UseFunctionInvocation()      // executes registered AIFunction tool calls (FR-028/029)
    .UseOpenTelemetry()           // gen-AI semantic conventions; no PII (Constitution telemetry gate)
    .Build();
```
**Contract**
- `StreamAsync` continues to delegate to `_chatClient.GetStreamingResponseAsync(messages, options, ct)` — now through the pipeline.
- App-defined tools are supplied per request via `options.Tools` (set by `InferenceParameters.ToChatOptions`); `UseFunctionInvocation()` executes them — **real** invocation, not faked (FR-029).
- The tool collection (`IEnumerable<AITool>`) is registered in DI and injected so `ChatService`/the surface can attach it to options.
- No loopback, no second manager; the existing generation lease in `FoundryChatClient` is unchanged (Constitution V).

---

## `FoundryChatClient` options/usage/tools wiring (`src/FoundryForge.Foundry/FoundryChatClient.cs` + `Internal/FoundryMessageMapper.cs`) — CHANGED

Resolves the existing `TODO(M4)` at `FoundryChatClient.cs` L65.

| Change | Detail | FR |
|--------|--------|----|
| Map `ChatOptions` → FL request | in `FoundryMessageMapper`: set temperature/max-tokens/top_p/frequency_penalty on the Betalgo request; attach tools; pass `response_format` **best-effort** (no enforcement) | FR-018/028/031 |
| Emit usage | when the FL/Betalgo stream exposes a usage/terminal frame (KI-007 #2 empty-`Choices` frame), yield a trailing `ChatResponseUpdate` carrying `UsageContent`; when absent, **emit nothing** (metric stays "unknown") | FR-016 |
| Emit finish reason | map the FL finish reason to `ChatResponseUpdate.FinishReason` (`ChatFinishReason`) when provided; else leave unset | FR-015 |
| Betalgo isolation | all FL/Betalgo request/response types stay inside `FoundryMessageMapper` (KI-007 #3) | FR-034 |

**Contract**
- `response_format` is passed through only as best-effort; **no** "guaranteed/enforced JSON" claim or toggle anywhere (FR-031, Constitution III/IV).
- Unsupported params (`top_k`/`min_p`/`repeat_penalty`/`seed`) are never written to the request — `ChatOptions` has no property for them (FR-019).
- The generation lease (`_gate.BeginGenerationAsync`) and cancellation token threading are unchanged (FR-011, Constitution V).
- **PIN (hardware)**: whether FL/Betalgo populates usage/finish-reason is a runtime unknown (research R2) — verified on Apple Silicon; honest "unknown" is a passing fallback.

---

## `IFoundryCatalogService` (`src/FoundryForge.Core/Abstractions/IFoundryCatalogService.cs`) — unchanged

| Method | M4 use | Story/FR |
|--------|--------|----------|
| `ListLoadedAsync(ct)` | authoritative loaded set → gate decision + active-model name; re-check on not-ready | US2 / FR-007/009 |
| `LoadAsync(alias, variantId?, ct)` | load-on-demand through the M1 `IModelStateGate` (no bypass) | US2 / FR-008 |
| `ListCachedAsync(ct)` | offer a cached model to load from the gate | US2 / FR-008 |
| `GetModelAsync(alias, ct)` | read `ContextLength` for the context estimate (R7) | US6 / FR-020 |

The example tool `get_loaded_model_info` (Foundry layer) also calls `ListLoadedAsync()` (R5).

---

## `IModelStateGate` (`src/FoundryForge.Core/Abstractions/IModelStateGate.cs`) — unchanged

- Chat streaming takes a generation lease (`BeginGenerationAsync`) inside `FoundryChatClient` so a concurrent load/unload **drains/rejects** rather than tearing the stream (Constitution V).
- A load rejected by an in-flight mutation throws `ModelBusyException`; the chat surface renders an honest "model busy, try again" — no hang (FR-010).

---

## DI registration deltas (`src/FoundryForge.App/MauiProgram.cs`) — CHANGED

```csharp
// NEW — chat history store; App injects the app-data chats directory (mirrors the FileSettingsService registration)
services.AddSingleton<IChatHistoryStore>(_ =>
    new FileChatHistoryStore(Path.Combine(FileSystem.AppDataDirectory, "chats")));

// NEW — genuine app tools attached to ChatOptions for UseFunctionInvocation()
services.AddSingleton<IReadOnlyList<AITool>>(sp => ChatTools.Create(sp));   // get_current_time, get_loaded_model_info

// EXISTING — ChatService now builds the pipeline internally (constructor change only; registration unchanged)
// services.AddSingleton<IChatService, ChatService>();
```

---

## Layering invariant (FR-034, Constitution V)
- `FoundryForge.App` references only `IChatService`, `IChatHistoryStore`, `IFoundryCatalogService`, `IModelStateGate`, and `FoundryForge.Core` seams + MEAI types. **No** `using Microsoft.AI.Foundry.Local` / Betalgo anywhere in `.App`.
- All FL/Betalgo types stay inside `FoundryForge.Foundry` (`FoundryChatClient`, `FoundryMessageMapper`, `ChatTools`'s FL touchpoints).
- All model mutations route through the single `IModelStateGate`; chat streams hold a generation lease.
- Chat is **in-process** via `FoundryChatClient` — no 127.0.0.1 loopback (FR-002).
