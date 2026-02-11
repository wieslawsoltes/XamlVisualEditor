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
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using AcpExtensionEntry = XamlVisualEditor.AcpExtension.AcpExtension;
using GitExtensionEntry = XamlVisualEditor.GitExtension.GitExtension;
using XamlVisualEditor.Language;
using XamlVisualEditor.Acp;
using XamlVisualEditor.App.Services;
using XamlVisualEditor.Lsp;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Workspace;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Language;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;
using XamlVisualEditor.Terminal;
using System.Threading;

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
            BuiltInExtensionHost extensionHost = Services.GetRequiredService<BuiltInExtensionHost>();
            extensionHost.ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();

            MainWindowViewModel mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);

            desktop.ShutdownRequested += (_, _) =>
            {
                mainVm.Dispose();
                extensionHost.Dispose();
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
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IAnimationPreviewService, AnimationPreviewService>();
        services.AddSingleton<IDebuggerService, DapDebuggerService>();
        services.AddSingleton<IDebugToolInstaller, DebugToolInstaller>();
        services.AddSingleton<IPtyProvider>(_ => PtyProviderFactory.CreateDefault());
        services.AddSingleton<ITerminalService, TerminalService>();
        services.AddSingleton<IAcpAgentHostFactory, AcpAgentHostFactory>();
        services.AddSingleton<IAcpSettings, AcpSettings>();
        services.AddSingleton<IAcpService, AcpService>();
        services.AddSingleton<IAcpProfileStore, AcpProfileStore>();
        services.AddSingleton<ISecretStore, OsSecretStore>();
        services.AddSingleton<IAcpOAuthDeviceFlowService, AcpOAuthDeviceFlowService>();

        services.AddSingleton<WorkspaceInfoService>();
        services.AddSingleton<IWorkspaceInfo>(sp => sp.GetRequiredService<WorkspaceInfoService>());
        services.AddSingleton<IWorkspaceInfoUpdater>(sp => sp.GetRequiredService<WorkspaceInfoService>());
        services.AddSingleton<IWorkspace, InMemoryWorkspace>();
        services.AddSingleton<IWindow, InMemoryWindow>();
        services.AddSingleton<ISettings, InMemorySettingsStore>();

        // LSP services
        services.AddSingleton<ILspSettingsStore, LspSettingsStore>();
        services.AddSingleton<ILspSettings>(sp => new LspSettings(
            sp.GetRequiredService<ILspSettingsStore>()));
        services.AddSingleton<ILanguageServiceRouter>(sp => new LspLanguageServiceRouter(
            sp.GetRequiredService<ILspSettings>().Servers,
            sp.GetService<ILoggerFactory>()));
        services.AddSingleton<ILanguageIntellisenseService, LspLanguageIntellisenseService>();

        // Language services
        services.AddSingleton<ILanguageIntellisenseService, CSharpLanguageService>();
        services.AddSingleton<ILanguageIntellisenseService, XamlLanguageService>();
        services.AddSingleton<ExtensionLanguageServiceRegistry>();
        services.AddSingleton<ILanguageIntellisenseService, ExtensionLanguageIntellisenseService>();
        services.AddSingleton<ILanguageIntellisenseRegistry, LanguageServiceRegistry>();

        // Transient services (stateless/lightweight)
        services.AddTransient<CompletionProviderRegistry>(_ => CompletionProviderRegistry.CreateDefault());
        services.AddTransient<AstNodeMap>();

        // Extension services
        services.AddSingleton<ICommands, CommandRegistry>();
        services.AddSingleton<IExtensionContributionRegistry, ExtensionContributionRegistry>();
        services.AddSingleton<IExtensionViewRegistry, ExtensionViewRegistry>();
        services.AddSingleton<IViews>(sp => sp.GetRequiredService<IExtensionViewRegistry>());
        services.AddSingleton<IExtensionLanguageServices>(sp => sp.GetRequiredService<ExtensionLanguageServiceRegistry>());
        services.AddSingleton<IEditorServices, EditorServicesAdapter>();
        services.AddSingleton<ExtensionPackageLoader>();
        services.AddSingleton<IExtensionPackageStore>(sp => new ExtensionPackageStore(
            ExtensionPackagePaths.GetInstalledRoot(),
            sp.GetRequiredService<ExtensionPackageLoader>()));
        services.AddSingleton<IExtensionPackageCatalog>(sp => new LocalExtensionPackageCatalog(
            ExtensionPackagePaths.GetCatalogRoot(),
            sp.GetRequiredService<ExtensionPackageLoader>()));
        services.AddSingleton<IExtensionStateStore>(_ => new FileExtensionStateStore(
            ExtensionPackagePaths.GetStateFilePath()));
        services.AddSingleton<IExtensionUpdateService, ExtensionUpdateService>();
        services.AddSingleton<IExtensionManager, ExtensionManager>();
        services.AddSingleton<BuiltInExtensionHost>();
        services.AddSingleton<IXveExtension, AcpExtensionEntry>();
        services.AddSingleton<IXveExtension, GitExtensionEntry>();

        // ViewModels (Singleton for shell-level, Transient for per-document)
        services.AddSingleton<MainWindowViewModel>();
    }
}
