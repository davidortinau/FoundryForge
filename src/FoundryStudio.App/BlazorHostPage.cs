using Microsoft.Maui.Controls;
using Microsoft.Maui.Platforms.MacOS.Controls;

namespace FoundryStudio.App;

public sealed class BlazorHostPage : ContentPage
{
    public BlazorHostPage()
    {
        Title = "FoundryStudio";

        var blazorWebView = new MacOSBlazorWebView
        {
            HostPage = "wwwroot/index.html",
        };

        blazorWebView.RootComponents.Add(new BlazorRootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Components.App),
        });

        Content = blazorWebView;
    }
}
