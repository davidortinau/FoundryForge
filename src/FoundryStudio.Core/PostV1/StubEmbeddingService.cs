using FoundryStudio.Core.Abstractions;

namespace FoundryStudio.Core.PostV1;

/// <summary>
/// Honest post-v1 stub (FR-013). Embeddings/RAG land in M6. <see cref="IsSupported"/> is false and the
/// operation throws — it never returns fake or empty data that would masquerade as a working feature.
/// FL-free, so it lives in Core and is unit-testable in the dylib-free test project.
/// </summary>
public sealed class StubEmbeddingService : IEmbeddingService
{
    public bool IsSupported => false;

    public Task<IReadOnlyList<float[]>> EmbedAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Embeddings are not implemented in v1 (planned for M6).");
}
