using System.Runtime.CompilerServices;
using System.Reactive.Concurrency;
using ReactiveUI;
using ReactiveUI.Builder;

namespace XamlVisualEditor.Tests.Integration;

/// <summary>
/// ReactiveUI 23 no longer initializes itself on first use; tests construct
/// view models without going through the application builder, so the module
/// initializer performs the equivalent setup once per test assembly.
/// </summary>
internal static class ReactiveUiBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithPlatformServices()
            .WithCoreServices()
            .BuildApp();

        // Upstream tests relied on RxApp's inline scheduling; keep that behavior
        // deterministic instead of depending on the platform scheduler.
        RxSchedulers.MainThreadScheduler = CurrentThreadScheduler.Instance;
    }
}
