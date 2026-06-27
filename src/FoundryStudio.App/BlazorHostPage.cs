using Microsoft.Maui.Controls;
using Microsoft.Maui.Platforms.MacOS.Controls;
using Microsoft.Maui.Platforms.MacOS.Platform;

namespace FoundryStudio.App;

public sealed class BlazorHostPage : ContentPage
{
    private readonly MacOSBlazorWebView _blazorWebView;
    private bool _toolbarBuilt;

    public BlazorHostPage()
    {
        Title = "FoundryStudio";

        _blazorWebView = new MacOSBlazorWebView
        {
            HostPage = "wwwroot/index.html",
        };

        _blazorWebView.RootComponents.Add(new BlazorRootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Components.App),
        });

        Content = _blazorWebView;

        // Build the native toolbar only AFTER the page is connected to a window/handler (Sherpa builds it from a
        // dispatched toolbar event, never in the constructor). Building too early wires the native NSToolbarItem's
        // target/action to an orphaned item, so AX still finds the button but real mouse clicks do nothing.
        HandlerChanged += (_, _) => ScheduleToolbarBuild();
        _blazorWebView.HandlerChanged += (_, _) => ScheduleToolbarBuild();
    }

    private void ScheduleToolbarBuild()
    {
        if (_toolbarBuilt) return;
        Dispatcher.Dispatch(() =>
        {
            if (_toolbarBuilt) return;
            if (Handler is null) return;
            _toolbarBuilt = true;
            BuildNativeToolbar();
        });
    }

    // Native macOS NSToolbar in the titlebar (maui-labs MacOSToolbar attached property). Each item bridges to the
    // Blazor app by clicking the matching hidden HTML control, so the toolbar uses the real titlebar space (no dead
    // band) while app logic stays in Blazor. (Sherpa BlazorContentPage.FullRebuildToolbar pattern.)
    private void BuildNativeToolbar()
    {
        var toggle = ToolbarButton("Toggle Sidebar", "sidebar.left", "[data-testid=\"sidebar-toggle\"]");
        var settings = ToolbarButton("Settings", "gearshape", "[data-testid=\"open-settings\"]");

        var layout = new List<MacOSToolbarLayoutItem>
        {
            MacOSToolbarLayoutItem.Item(toggle),
            MacOSToolbarLayoutItem.FlexibleSpace,
            MacOSToolbarLayoutItem.Item(settings),
        };

        // ORDER IS LOAD-BEARING (Sherpa lines 908-923): set the content layout FIRST, then mutate ToolbarItems
        // LAST so the final RefreshToolbar sees the complete state and wires each native button's mouse
        // target/action. Reversing this leaves the mouse path bound to a stale item (AX works, mouse doesn't).
        MacOSToolbar.SetContentLayout(this, layout);

        ToolbarItems.Clear();
        ToolbarItems.Add(toggle);
        ToolbarItems.Add(settings);
    }

    private ToolbarItem ToolbarButton(string text, string? sfSymbol, string selector)
        => new()
        {
            Text = text,
            IconImageSource = sfSymbol,
            Command = new Command(() => Click(selector)),
        };

    private void Click(string selector)
    {
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                if (_blazorWebView.Handler?.PlatformView is WebKit.WKWebView web)
                {
                    await web.EvaluateJavaScriptAsync($"document.querySelector('{selector}')?.click()");
                }
            }
            catch
            {
                // Non-fatal.
            }
        });
    }
}
