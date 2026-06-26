# Contract: Post-v1 service interfaces (stubbed, non-faking)

**Project**: `FoundryStudio.Core/Abstractions` (interfaces) · stub impls
`FoundryStudio.Foundry/PostV1/Stub*Service.cs`
**Satisfies**: FR-013 · SC-010 · Constitution IV (capability honesty) · PLAN.md lines 63–66

These interfaces are **defined in M1** so M5/M6 can implement them without reshaping the
dependency graph, but their v1 implementations are **honest stubs** — they signal
"not implemented in v1," never fake behavior or return empty data implying support.

```csharp
namespace FoundryStudio.Core.Abstractions;

/// Embeddings (powers RAG, post-v1 / M6).
public interface IEmbeddingService
{
    bool IsSupported { get; }            // false in v1
    Task<IReadOnlyList<float[]>> EmbedAsync(IEnumerable<string> inputs,
        CancellationToken cancellationToken = default); // stub throws NotSupportedException
}

/// Speech-to-text (FL Whisper, post-v1 / M6).
public interface ITranscriptionService
{
    bool IsSupported { get; }            // false in v1
    Task<string> TranscribeAsync(Stream audio,
        CancellationToken cancellationToken = default); // stub throws NotSupportedException
}

/// Exposed local OpenAI-compatible server for EXTERNAL tools only (M5).
/// Interface defined now; the toggle/UI and impl are M5 (out of M1 scope, FR-019).
public interface ILocalServerService
{
    bool IsSupported { get; }            // false in v1 (M1)
    Task<IReadOnlyList<string>> StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<string> Urls { get; }
}
```

### Behavioral contract

| # | Given | When | Then |
|---|---|---|---|
| 1 | A v1 build | any of these resolved from DI | resolves to a stub whose `IsSupported == false` (FR-013). |
| 2 | A consumer calls a stub operation | invoked | throws `NotSupportedException("Not implemented in v1 …")` — **does not** return fake/empty data implying support (FR-013, Constitution IV). |
| 3 | The dependency graph | M5/M6 swaps in a real impl | no consumer reshaping needed — the interface was stable from M1 (spec Assumptions). |
| 4 | `ILocalServerService` | considered in M1 | the **exposed server toggle/UI is M5**, not M1 (FR-019); the FL constraints (localhost-only, no auth, no LAN bind) are surfaced honestly when M5 lands (PLAN.md 125), never faked. |

### Capability-honesty guard (Constitution IV, FR-018/SC-010)
No UI or toggle is shipped in M1 for any of these (or for any unsupported FL feature). The
stubs make "not yet implemented" explicit and visible rather than presenting a dead surface.

### Test notes
Stub behavior (`IsSupported == false`, operations throw `NotSupportedException`) is trivially
unit-testable; the stubs live in `Foundry` but the **interfaces** are in `Core`, so consumers
and tests code against the abstractions.
