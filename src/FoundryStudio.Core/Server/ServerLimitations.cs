namespace FoundryStudio.Core.Server;

/// <summary>
/// The exposed server's real, plainly-stated limitations (US6). Pure DATA rendered as text — there is no
/// setter and no control for any of these, because Foundry Local does not support them and shipping a dead
/// auth/LAN toggle would be a fabricated capability (Constitution III, FR-019/020/021).
/// </summary>
public static class ServerLimitations
{
    public const string LocalhostOnly =
        "Localhost only. The server binds to 127.0.0.1 and is reachable only from this Mac.";

    public const string NoAuth =
        "No authentication. Foundry Local does not provide API-key or token auth for the local server.";

    public const string NoLanBind =
        "No LAN binding. The server cannot be exposed on your network (no 0.0.0.0 bind) in Foundry Local.";

    public const string ExternalOnly =
        "For external tools only. In-app chat runs in-process and is unaffected by whether this server is on or off.";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        LocalhostOnly,
        NoAuth,
        NoLanBind,
        ExternalOnly
    };
}
