# Contract: `IModelStateGate` (load/unload concurrency gate)

**Project**: `FoundryStudio.Core/Abstractions` (interface) · primitive
`FoundryStudio.Core/Concurrency/ModelStateGate.cs` (pure, FL-free)
**Satisfies**: FR-008, FR-009, FR-010 · SC-004 · Constitution V · PLAN.md line 75 · DEC-004

Serializes model-state mutations and drains-or-rejects them against any active stream **on the
same model**. Pure-logic — unit-testable with no native dylib (FR-016, SC-008). One gate backs
the one manager (both the in-process path and the future exposed server).

```csharp
namespace FoundryStudio.Core.Abstractions;

public enum MutationPolicy { Drain, Reject }

public interface IModelStateGate
{
    /// Marks a streaming generation active on `modelId`. Dispose the lease when the stream
    /// ends. Wrapped around every chat stream so the gate knows the model is busy.
    Task<IAsyncDisposable> BeginGenerationAsync(string modelId,
        CancellationToken cancellationToken = default);

    /// Runs a model-state mutation (load/unload) under the per-model mutation lock.
    /// Drain: waits (bounded) for active generations to reach zero, then runs `op`.
    /// Reject: throws ModelBusyException if a generation is active on `modelId`.
    Task MutateAsync(string modelId, Func<Task> op, MutationPolicy policy,
        CancellationToken cancellationToken = default);
}

/// Honest, actionable rejection — never a fake success or silent no-op.
public sealed class ModelBusyException : Exception
{
    public ModelBusyException(string modelId)
        : base($"Model '{modelId}' has an active generation; load/unload was rejected to "
             + "avoid tearing the stream. Stop the generation and retry.") { }
}
```

### Behavioral contract

| # | Given | When | Then |
|---|---|---|---|
| 1 | Active generation streaming on a loaded model | unload (or displacing load) requested for **that** model | operation **waits for drain** or is **rejected**; model state is never mutated mid-stream (FR-008, SC-004). |
| 2 | No active generation on a model | load or unload requested | proceeds and completes (FR-008). |
| 3 | Two mutations on the same model requested concurrently | gate processes them | **serialized** by the per-model mutation lock; never two at once (FR-009, SC-004). |
| 4 | Mutation rejected because a stream is active | rejection surfaced | typed `ModelBusyException` — honest/actionable, **not** a fake success / silent no-op (FR-010, SC-004). |
| 5 | One manager backs both the in-process path and the (future) server | gate exercised from either | same contract governs both (FR-009, Constitution V). |
| 6 | Active stream on model **A** | load/unload requested on model **B** | **allowed** — per-model isolation, the contract protects the active-stream model's state, not all models globally (edge case). |

### Test notes (Story 2 independent test — no UI, no dylib)
- Simulate an in-flight generation (acquire a `BeginGenerationAsync` lease), then request
  `MutateAsync` with `Drain` (asserts it waits for lease disposal) and with `Reject` (asserts
  `ModelBusyException`).
- Fire concurrent `MutateAsync` on one model; assert observed concurrent mutations == 0
  (SC-004).
- Mutate model B while model A has an active lease; assert it proceeds (per-model isolation).
