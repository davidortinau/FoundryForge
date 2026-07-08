namespace FoundryForge.Core.Server;

/// <summary>
/// A snapshot of the exposed server's honest state. <see cref="Urls"/> is non-empty ONLY when
/// <see cref="ServerState.Running"/> — an Error/Stopped status carries no addresses (no fabricated
/// endpoint, FR-006/008). <see cref="Message"/> names the real diagnosed cause for Error/busy, else null.
/// </summary>
public sealed record ServerStatus(
    ServerState State,
    IReadOnlyList<string> Urls,
    string? Message = null)
{
    public static ServerStatus Stopped { get; } = new(ServerState.Stopped, Array.Empty<string>());

    public bool IsRunning => State == ServerState.Running;

    public bool IsBusy => State is ServerState.Starting or ServerState.Stopping;
}
