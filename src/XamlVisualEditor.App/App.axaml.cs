using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.CSharp.Language;
using XamlVisualEditor.Language;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Workspace;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Language;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;

namespace XamlVisualEditor.App;

/// <summary>
/// Application entry point and composition root.
/// </summary>
public sealed class App : Application
{
    /// <summary>
    /// Gets the service provider for DI.
    /// </summary>
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build the DI container
        ServiceCollection services = new();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindowViewModel mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);

            desktop.ShutdownRequested += (_, _) =>
            {
                mainVm.Dispose();
                (Services as IDisposable)?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Core services (Singleton — thread-safe, shared)
        services.AddSingleton<IXamlParsingService, XamlParsingService>();
        services.AddSingleton<IXamlSerializationService, XamlSerializationService>();
        services.AddSingleton<ITypeMetadataService, TypeMetadataService>();
        services.AddSingleton<IWorkspaceService, WorkspaceService>();

        // Language services
        services.AddSingleton<ILanguageIntellisenseService, CSharpLanguageService>();
        services.AddSingleton<ILanguageIntellisenseService, XamlLanguageService>();
        services.AddSingleton<ILanguageIntellisenseRegistry, LanguageServiceRegistry>();

        // Transient services (stateless/lightweight)
        services.AddTransient<CompletionProviderRegistry>(_ => CompletionProviderRegistry.CreateDefault());
        services.AddTransient<AstNodeMap>();

        // ViewModels (Singleton for shell-level, Transient for per-document)
        services.AddSingleton<MainWindowViewModel>();
    }
}
