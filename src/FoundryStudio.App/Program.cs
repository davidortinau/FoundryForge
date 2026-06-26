using AppKit;

namespace FoundryStudio.App;

public static class Program
{
    public static void Main(string[] args)
    {
        NSApplication.Init();
        NSApplication.SharedApplication.Delegate = new MauiMacOSApp();
        NSApplication.Main(args);
    }
}
