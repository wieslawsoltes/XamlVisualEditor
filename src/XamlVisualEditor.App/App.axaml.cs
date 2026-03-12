using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions.Debugging;
using XamlVisualEditor.Core.Logging;
using XamlVisualEditor.CSharp.Language;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using AcpExtensionEntry = XamlVisualEditor.AcpExtension.AcpExtension;
using GitExtensionEntry = XamlVisualEditor.GitExtension.GitExtension;
using IdeBridgeExtensionEntry = XamlVisualEditor.IdeBridgeExtension.IdeBridgeExtension;
using McpExtensionEntry = XamlVisualEditor.McpExtension.McpExtension;
using VscodeCompatExtensionEntry = XamlVisualEditor.VscodeCompatExtension.VscodeCompatExtension;
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
using DotNetTemplatesExtensionEntry = XamlVisualEditor.DotNetTemplatesExtension.DotNetTemplatesExtension;
using DapDebuggingExtensionEntry = XamlVisualEditor.Debugging.DapExtension.DapDebuggingExtension;
using DotNetSdkDebuggingExtensionEntry = XamlVisualEditor.Debugging.DotNetSdkExtension.DotNetSdkDebuggingExtension;
using ExtensionSystemIconServiceEntry = XamlVisualEditor.FileExplorerExtension.ExtensionSystemIconService;
using FileExplorerExtensionEntry = XamlVisualEditor.FileExplorerExtension.FileExplorerExtension;
using SolutionExplorerExtensionEntry = XamlVisualEditor.SolutionExplorerExtension.SolutionExplorerExtension;
using ToolboxExtensionEntry = XamlVisualEditor.ToolboxExtension.ToolboxExtension;
using TreeInspectorExtensionEntry = XamlVisualEditor.TreeInspectorExtension.TreeInspectorExtension;
using NavigationExtensionEntry = XamlVisualEditor.NavigationExtension.NavigationExtension;
using AnimationEditorExtensionEntry = XamlVisualEditor.AnimationEditorExtension.AnimationEditorExtension;
using CollaborationExtensionEntry = XamlVisualEditor.CollaborationExtension.CollaborationExtension;
using DebugSettingsExtensionEntry = XamlVisualEditor.DebugSettingsExtension.DebugSettingsExtension;
using LspSettingsExtensionEntry = XamlVisualEditor.LspSettingsExtension.LspSettingsExtension;
using PropertyEditorExtensionEntry = XamlVisualEditor.PropertyEditorExtension.PropertyEditorExtension;
using OutputExtensionEntry = XamlVisualEditor.OutputExtension.OutputExtension;
using XamlEditorExtensionEntry = XamlVisualEditor.XamlEditorExtension.XamlEditorExtension;
using WorkspaceExtensionEntry = XamlVisualEditor.WorkspaceExtension.WorkspaceExtension;

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
            MainWindow mainWindow = new(mainVm);
            desktop.MainWindow = mainWindow;

            MainWindowProvider windowProvider = Services.GetRequiredService<MainWindowProvider>();
            windowProvider.MainWindow = mainWindow;
            if (Services.GetService<IWindow>() is AppWindow appWindow)
            {
                appWindow.SyncStatusBarItems();
            }

            string? startupPath = StartupArgs.GetWorkspacePath(desktop.Args);
            if (!string.IsNullOrWhiteSpace(startupPath))
            {
                Dispatcher.UIThread.Post(() => _ = mainVm.OpenFileAsync(startupPath));
            }

            desktop.ShutdownRequested += (_, _) =>
            {
                mainWindow.DataContext = null;
                mainWindow.Content = null;
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
        services.AddSingleton<IDotNetCli, DotNetCliRunner>();
        services.AddSingleton<IDotNetTemplateService, DotNetTemplateService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IAnimationPreviewService, AnimationPreviewService>();
        services.AddSingleton<IDebugToolInstaller, DebugToolInstaller>();
        services.AddSingleton<IPtyProvider>(_ => PtyProviderFactory.CreateDefault());
        services.AddSingleton<ITerminalEmulatorFactory, ManagedTerminalEmulatorFactory>();
        services.AddSingleton<ITerminalService, TerminalService>();
        services.AddSingleton<IAcpAgentHostFactory, AcpAgentHostFactory>();
        services.AddSingleton<IAcpSettings, AcpSettings>();
        services.AddSingleton<IAcpService, AcpService>();
        services.AddSingleton<IAcpProfileStore, AcpProfileStore>();
        services.AddSingleton<ISecretStore, OsSecretStore>();
        services.AddSingleton<IAcpOAuthDeviceFlowService, AcpOAuthDeviceFlowService>();
        services.AddSingleton<IDebuggerServiceRegistry, DebuggerServiceRegistry>();

        services.AddSingleton<WorkspaceInfoService>();
        services.AddSingleton<IWorkspaceInfo>(sp => sp.GetRequiredService<WorkspaceInfoService>());
        services.AddSingleton<IWorkspaceInfoUpdater>(sp => sp.GetRequiredService<WorkspaceInfoService>());
        services.AddSingleton<IWorkspace, FileSystemWorkspace>();
        services.AddSingleton<IWindow, AppWindow>();
        services.AddSingleton<ISettings, InMemorySettingsStore>();
        services.AddSingleton<ISystemIconService, ExtensionSystemIconServiceEntry>();
        services.AddSingleton<IPropertyEditorRegistry, PropertyEditorRegistry>();
        services.AddSingleton<IWorkspaceCommands>(sp => sp.GetRequiredService<MainWindowViewModel>());
        services.AddSingleton<MainWindowProvider>();
        services.AddSingleton<IFolderPicker, AppFolderPicker>();
        services.AddSingleton<IDialogHost, AppDialogHost>();
        services.AddSingleton<IWorkspaceHost, AppWorkspaceHost>();

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
        services.AddSingleton<ICommandMetadataRegistry, CommandMetadataRegistry>();
        services.AddSingleton<IExtensionContributionRegistry, ExtensionContributionRegistry>();
        services.AddSingleton<IExtensionViewRegistry, ExtensionViewRegistry>();
        services.AddSingleton<IViews>(sp => sp.GetRequiredService<IExtensionViewRegistry>());
        services.AddSingleton<IExtensionLanguageServices>(sp => sp.GetRequiredService<ExtensionLanguageServiceRegistry>());
        services.AddSingleton<ILanguageNavigationService, LanguageNavigationServiceAdapter>();
        services.AddSingleton<INavigationHistoryService, NavigationHistoryServiceAdapter>();
        services.AddSingleton<IAnimationEditorHost, AnimationEditorHostAdapter>();
        services.AddSingleton<CollaborationPanelHostAdapter>();
        services.AddSingleton<ICollaborationHost>(sp => sp.GetRequiredService<CollaborationPanelHostAdapter>());
        services.AddSingleton<ICollaborationPanelHost>(sp => sp.GetRequiredService<CollaborationPanelHostAdapter>());
        services.AddSingleton<ISolutionExplorerPanelHost, SolutionExplorerPanelHostAdapter>();
        services.AddSingleton<IDebugSettingsHost, DebugSettingsHostAdapter>();
        services.AddSingleton<ILspSettingsHost, LspSettingsHostAdapter>();
        services.AddSingleton<IEditorServices, EditorServicesAdapter>();
        services.AddSingleton<IDesignerHost, ShellDesignerHost>();
        services.AddSingleton<IWorkspaceModel, WorkspaceModelAdapter>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsServiceAdapter>();
        services.AddSingleton<ITerminalBridge, TerminalBridgeAdapter>();
        services.AddSingleton<IExtensionViewHost, ExtensionViewHostAdapter>();
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
        services.AddSingleton<IXveExtension, IdeBridgeExtensionEntry>();
        services.AddSingleton<IXveExtension, McpExtensionEntry>();
        services.AddSingleton<IXveExtension, VscodeCompatExtensionEntry>();
        services.AddSingleton<IXveExtension, DotNetTemplatesExtensionEntry>();
        services.AddSingleton<IXveExtension, FileExplorerExtensionEntry>();
        services.AddSingleton<IXveExtension, SolutionExplorerExtensionEntry>();
        services.AddSingleton<IXveExtension, ToolboxExtensionEntry>();
        services.AddSingleton<IXveExtension, PropertyEditorExtensionEntry>();
        services.AddSingleton<IXveExtension, OutputExtensionEntry>();
        services.AddSingleton<IXveExtension, NavigationExtensionEntry>();
        services.AddSingleton<IXveExtension, XamlEditorExtensionEntry>();
        services.AddSingleton<IXveExtension, TreeInspectorExtensionEntry>();
        services.AddSingleton<IXveExtension, AnimationEditorExtensionEntry>();
        services.AddSingleton<IXveExtension, CollaborationExtensionEntry>();
        services.AddSingleton<IXveExtension, DebugSettingsExtensionEntry>();
        services.AddSingleton<IXveExtension, LspSettingsExtensionEntry>();
        services.AddSingleton<IXveExtension, WorkspaceExtensionEntry>();
        services.AddSingleton<IXveExtension, DapDebuggingExtensionEntry>();
        services.AddSingleton<IXveExtension, DotNetSdkDebuggingExtensionEntry>();

        // ViewModels (Singleton for shell-level, Transient for per-document)
        services.AddSingleton<MainWindowViewModel>();
    }
}
