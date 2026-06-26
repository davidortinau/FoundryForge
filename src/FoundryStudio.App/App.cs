using Microsoft.Maui.Controls;

namespace FoundryStudio.App;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new BlazorHostPage())
        {
            Title = "FoundryStudio",
            Width = 1080,
            Height = 760,
        };
    }
}
