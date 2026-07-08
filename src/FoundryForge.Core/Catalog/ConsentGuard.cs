namespace FoundryForge.Core.Catalog;

/// <summary>
/// Consent enforcement for destructive operations on protected user data (Constitution IV). Pure/dylib-free
/// so the gate is unit-testable without a native dependency. Throws BEFORE any FL/dylib work occurs.
/// </summary>
public static class ConsentGuard
{
    public static void RequireConfirmed(bool userConfirmed, string action)
    {
        if (!userConfirmed)
        {
            throw new InvalidOperationException($"{action} requires explicit user confirmation (protected user data).");
        }
    }
}
