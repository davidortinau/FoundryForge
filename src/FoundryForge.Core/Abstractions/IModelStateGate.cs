namespace FoundryForge.Core.Abstractions;

public enum MutationPolicy
{
    Drain,
    Reject,
}

public interface IModelStateGate
{
    Task<IAsyncDisposable> BeginGenerationAsync(string modelId, CancellationToken cancellationToken = default);

    Task MutateAsync(string modelId, MutationPolicy policy, Func<Task> mutation, CancellationToken cancellationToken = default);
}

public sealed class ModelBusyException : Exception
{
    public ModelBusyException(string modelId)
        : base($"Model '{modelId}' has an active generation; load/unload was rejected to avoid tearing the stream. Stop the generation and retry.")
    {
        ModelId = modelId;
    }

    public string ModelId { get; }
}
