using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Core.Debugging;
using XamlVisualEditor.Core.Logging;
using XamlVisualEditor.Debugging.Dap;
using XamlVisualEditor.CSharp.Language;
using XamlVisualEditor.Language;
using XamlVisualEditor.App.Services;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Workspace;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Language;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;
using XamlVisualEditor.Terminal;

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
        OutputLogSinkAccessor outputLogSinkAccessor = new();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Sink(new OutputPanelLogSink(outputLogSinkAccessor))
            .CreateLogger();

        services.AddSingleton<IOutputLogSinkAccessor>(outputLogSinkAccessor);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        // Core services (Singleton — thread-safe, shared)
        services.AddSingleton<IXamlParsingService, XamlParsingService>();
        services.AddSingleton<IXamlSerializationService, XamlSerializationService>();
        services.AddSingleton<ITypeMetadataService, TypeMetadataService>();
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IAnimationPreviewService, AnimationPreviewService>();
        services.AddSingleton<IDebuggerService, DapDebuggerService>();
        services.AddSingleton<IDebugToolInstaller, DebugToolInstaller>();
        services.AddSingleton<IPtyProvider>(_ => PtyProviderFactory.CreateDefault());
        services.AddSingleton<ITerminalService, TerminalService>();

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
