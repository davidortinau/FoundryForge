using FoundryStudio.Core.Abstractions;

namespace FoundryStudio.Core.PostV1;

/// <summary>
/// Honest stub for the exposed local OpenAI server (FR-013). Foundry Local supports it
/// (<c>StartWebServiceAsync</c>), but it is wired as the v1 "wow" toggle in M5 — not in the M1 foundation.
/// <see cref="IsSupported"/> is false until M5; operations throw rather than pretending to run a server.
/// </summary>
public sealed class StubLocalServerService : ILocalServerService
{
    public bool IsSupported => false;

    public IReadOnlyList<string> Urls => Array.Empty<string>();

    public Task<IReadOnlyList<string>> StartAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The exposed local server is wired in M5.");

    public Task StopAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The exposed local server is wired in M5.");
}
