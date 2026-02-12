using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(XamlVisualEditor.Tests.Unit.HeadlessTestAppBuilder))]

namespace XamlVisualEditor.Tests.Unit;

public sealed class HeadlessTestApp : Application
{
}

public static class HeadlessTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
