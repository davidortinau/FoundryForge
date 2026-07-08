using FoundryForge.Core.Catalog;
using Xunit;

namespace FoundryForge.Tests;

/// <summary>
/// US3 / Constitution IV (SC-006): consent gate for destructive cache deletion. Dylib-free — proves the
/// guard throws before any FL/native work, removing nothing.
/// </summary>
public class DeleteConsentGateTests
{
    [Fact]
    public void Unconfirmed_delete_throws_and_does_nothing()
    {
        var ex = Assert.Throws<System.InvalidOperationException>(
            () => ConsentGuard.RequireConfirmed(userConfirmed: false, "Deleting cached model 'qwen2.5-0.5b'"));
        Assert.Contains("explicit user confirmation", ex.Message);
    }

    [Fact]
    public void Confirmed_delete_passes_the_gate()
    {
        ConsentGuard.RequireConfirmed(userConfirmed: true, "Deleting cached model 'qwen2.5-0.5b'"); // no throw
    }
}
