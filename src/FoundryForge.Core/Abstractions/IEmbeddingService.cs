namespace FoundryForge.Core.Abstractions;

public interface IEmbeddingService
{
    bool IsSupported { get; }

    Task<IReadOnlyList<float[]>> EmbedAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default);
}
