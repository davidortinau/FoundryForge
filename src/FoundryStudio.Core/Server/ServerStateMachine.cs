namespace FoundryStudio.Core.Server;

/// <summary>
/// Validates server lifecycle transitions and the pilot-light rule (US1, US4). Pure, deterministic,
/// dylib-free. The pilot light is lit ONLY in <see cref="ServerState.Running"/> — never a fabricated
/// "lit" state (Constitution III, FR-013).
/// </summary>
public static class ServerStateMachine
{
    public static bool CanTransition(ServerState from, ServerState to)
    {
        if (to == ServerState.Error)
        {
            return true; // any state can fault into Error
        }

        return (from, to) switch
        {
            (ServerState.Stopped, ServerState.Starting) => true,
            (ServerState.Starting, ServerState.Running) => true,
            (ServerState.Running, ServerState.Stopping) => true,
            (ServerState.Stopping, ServerState.Stopped) => true,
            (ServerState.Error, ServerState.Starting) => true,
            (ServerState.Error, ServerState.Stopped) => true,
            _ => false
        };
    }

    public static bool PilotLightLit(ServerState state) => state == ServerState.Running;
}
