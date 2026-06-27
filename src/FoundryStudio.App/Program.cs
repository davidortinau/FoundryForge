using AppKit;

namespace FoundryStudio.App;

public static class Program
{
    public static void Main(string[] args)
    {
        NSApplication.Init();
        var app = NSApplication.SharedApplication;
        app.Delegate = new MauiMacOSApp();
        // Make this a normal, foreground GUI app: dock icon + a window that can be activated. Without this
        // the directly-launched binary runs as a background/accessory process (no dock icon, window never
        // comes to the front), which makes interactive review impossible. (maui-labs AppKit head gap.)
        app.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        app.ActivateIgnoringOtherApps(true);
        NSApplication.Main(args);
    }
}
