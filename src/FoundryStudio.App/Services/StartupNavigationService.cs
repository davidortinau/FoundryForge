namespace FoundryStudio.App.Services;

/// <summary>
/// Tracks whether the one-time startup landing redirect has fired.
/// The first navigation to "/" checks cached models and may redirect to "/server".
/// Subsequent navigations to "/" (user explicit clicks) skip the redirect.
/// </summary>
public sealed class StartupNavigationService
{
    private bool _redirectConsumed;

    /// <summary>
    /// Returns true the FIRST time this is called (the startup check), false thereafter.
    /// Thread-safe for single-threaded Blazor dispatch loop.
    /// </summary>
    public bool ShouldCheckStartupRedirect()
    {
        if (_redirectConsumed) return false;
        _redirectConsumed = true;
        return true;
    }
}
