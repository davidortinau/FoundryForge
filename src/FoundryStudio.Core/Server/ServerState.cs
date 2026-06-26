namespace FoundryStudio.Core.Server;

/// <summary>Honest lifecycle of the exposed local OpenAI-compatible server (US1, US4).</summary>
public enum ServerState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}
