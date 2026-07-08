using Foundation;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platforms.MacOS.Platform;

namespace FoundryForge.App;

[Register("MauiMacOSApp")]
public sealed class MauiMacOSApp : MacOSMauiApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
