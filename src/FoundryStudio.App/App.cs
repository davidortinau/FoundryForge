using AppKit;
using Microsoft.Maui.Controls;

namespace FoundryStudio.App;

public sealed class App : Application
{
    private static NSWindow? _nativeWindow;
    private static bool _titlebarSeeded;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new BlazorHostPage())
        {
            Title = "FoundryStudio",
            Width = 1080,
            Height = 760,
        };

        // The native NSToolbar (built in BlazorHostPage) occupies the titlebar, so the standard macOS layout
        // (toolbar in the titlebar, WebView content below it) is correct — no dead space, and the WebView
        // does NOT overlap the toolbar buttons. We only sync the titlebar/toolbar APPEARANCE to the in-app
        // theme (light/dark) via the JS bridge below.
        window.Activated += (_, _) => Resolve(window);
        window.HandlerChanged += (_, _) => Resolve(window);

        return window;
    }

    private static void Resolve(Window window)
        => NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            _nativeWindow = ResolveNSWindow(window);
            if (_nativeWindow is { } w && !_titlebarSeeded)
            {
                // First paint, theme-independent: make the titlebar transparent so it shows the window
                // background, and seed the background to the light canvas so the band blends with the body
                // immediately. The JS theme bridge (SetWindowBackgroundColor) corrects this per the live theme.
                _titlebarSeeded = true;
                w.TitlebarAppearsTransparent = true;
                w.BackgroundColor = NSColor.FromRgb(0.969f, 0.957f, 0.937f); // #F7F4EF light canvas
            }
        });

    /// <summary>
    /// Called from theme.js to drive the native window APPEARANCE from the in-app theme MODE.
    /// "system" must clear the forced appearance (null) so the window inherits the OS light/dark
    /// setting — otherwise a previously-forced DarkAqua sticks and the WebView's prefers-color-scheme
    /// keeps reporting dark, leaving the app stuck dark when switching Dark -> System.
    /// </summary>
    [Microsoft.JSInterop.JSInvokable]
    public static void SetWindowThemeMode(string mode)
    {
        try
        {
            var w = _nativeWindow ?? NSApplication.SharedApplication.KeyWindow ?? NSApplication.SharedApplication.MainWindow;
            if (w is null) return;
            NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
            {
                w.Appearance = mode switch
                {
                    "light" => NSAppearance.GetAppearance(NSAppearance.NameAqua),
                    "dark" => NSAppearance.GetAppearance(NSAppearance.NameDarkAqua),
                    _ => null, // "system" — inherit the OS appearance
                };
            });
        }
        catch { }
    }

    /// <summary>
    /// Called from theme.js to tint the titlebar/toolbar to the live body canvas color so the band
    /// blends with the content. This only sets the BACKGROUND — appearance is owned by SetWindowThemeMode.
    /// </summary>
    [Microsoft.JSInterop.JSInvokable]
    public static void SetWindowBackgroundColor(string cssRgb)
    {
        try
        {
            var w = _nativeWindow ?? NSApplication.SharedApplication.KeyWindow ?? NSApplication.SharedApplication.MainWindow;
            if (w is null || string.IsNullOrWhiteSpace(cssRgb)) return;
            var nums = System.Text.RegularExpressions.Regex.Matches(cssRgb, "[0-9]+");
            if (nums.Count < 3) return;
            int ri = int.Parse(nums[0].Value), gi = int.Parse(nums[1].Value), bi = int.Parse(nums[2].Value);
            float r = ri / 255f, g = gi / 255f, b = bi / 255f;
            NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
            {
                w.TitlebarAppearsTransparent = true;
                w.BackgroundColor = NSColor.FromRgb((nfloat)r, (nfloat)g, (nfloat)b);
            });
        }
        catch { }
    }

    private static NSWindow? ResolveNSWindow(Window window)
    {
        var pv = window.Handler?.PlatformView;
        switch (pv)
        {
            case NSWindow w: return w;
            case NSWindowController wc when wc.Window is { } w2: return w2;
            case NSViewController vc when vc.View?.Window is { } w3: return w3;
            case NSView v when v.Window is { } w4: return w4;
        }
        if (window.Page?.Handler?.PlatformView is NSView cv && cv.Window is { } w5) return w5;
        var nsApp = NSApplication.SharedApplication;
        return nsApp.KeyWindow ?? nsApp.MainWindow;
    }
}
