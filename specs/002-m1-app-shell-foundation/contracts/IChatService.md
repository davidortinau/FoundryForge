# Contract: `IChatService` + in-process `IChatClient` adapter

**Project**: `FoundryStudio.Core/Abstractions` (`IChatService`) · impls
`FoundryStudio.Foundry/FoundryChatClient.cs` (`IChatClient` adapter) + `ChatService.cs`
**Satisfies**: FR-012, FR-018 · SC-006, SC-010 · PLAN.md lines 59–62, 74 · DEC-004 · E4 (M0d)

A thin in-process adapter over the Foundry Local SDK presenting a conventional
Microsoft.Extensions.AI `IChatClient` surface that standard middleware composes around — with
**no loopback HTTP socket**. The exposed local server is for external tools only and is **out
of M1 scope** (M5).

```csharp
namespace FoundryStudio.Core.Abstractions;

using Microsoft.Extensions.AI; // ChatMessage, ChatResponse, ChatResponseUpdate, ChatOptions

public interface IChatService
{
    /// In-process streaming completion over the loaded model. No socket.
    IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

The implementation is the M0d-proven adapter, promoted:

```csharp
// FoundryStudio.Foundry
public sealed class FoundryChatClient : IChatClient   // Microsoft.Extensions.AI
{
    // Backed by IModel.GetChatClientAsync() -> CompleteChatStreamingAsync (in-process).
    // Maps MEAI ChatMessage <-> the FL request message type.
    // Acquires an IModelStateGate generation lease around each stream (so load/unload
    // cannot race the active stream — see IModelStateGate).
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken ct);
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken ct);
    public object? GetService(Type serviceType, object? serviceKey = null);
    public void Dispose();
}
```

### Behavioral contract

| # | Given | When | Then |
|---|---|---|---|
| 1 | A loaded model | a completion is requested through the adapter | served **in-process** over the FL SDK; **no loopback HTTP socket** is bound for the in-process chat path (FR-012, SC-006). |
| 2 | The adapter | standard MEAI middleware composed around it | it presents a conventional `IChatClient`, so `AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()` works (FR-012; wiring lands in M4, seam reserved now). |
| 3 | A streaming completion | it runs | the stream holds an `IModelStateGate` generation lease for its model (so load/unload drains/rejects per that contract). |
| 4 | Structured output requested (`response_format`) | served | treated as **best-effort only** — accepted but not enforced (E4 / M0d); **no "guaranteed JSON"** surface or promise (FR-018, SC-010). |
| 5 | Tool/function calling | requested | the path supports MEAI `UseFunctionInvocation` (M0d confirmed `tools` SUPPORTED); full chat tool wiring is M4. |

### Capability honesty (FR-018, SC-010)
- `response_format` is best-effort; do **not** design a "guaranteed JSON" capability or any
  toggle for unsupported FL features (server auth/LAN, GGUF, top_k/min_p/seed).
- M1 provides the **adapter seam**, not the M4 streaming chat UI (FR-019).

### Test notes
The pure mapping logic (MEAI ↔ FL message shape) can be unit-tested in isolation; the live
in-process stream + "no socket bound" assertion is verified at the M1 service smoke / DevFlow
end-to-end (the M0d slice already proved a streamed reply in-process).
