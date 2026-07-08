namespace FoundryForge.Core.Abstractions;

/// <summary>
/// One Foundry Local lifecycle, shared by all consumers. Initialization is awaited, never blocked.
/// </summary>
public interface IFoundryLifecycle : IAsyncDisposable
{
    Task ReadyAsync(CancellationToken cancellationToken = default);

    Task<object> GetManagerAsync(CancellationToken cancellationToken = default);

    FoundryReadyState State { get; }
}

public enum FoundryReadyState
{
    Uninitialized,
    Initializing,
    Ready,
    Failed,
}
