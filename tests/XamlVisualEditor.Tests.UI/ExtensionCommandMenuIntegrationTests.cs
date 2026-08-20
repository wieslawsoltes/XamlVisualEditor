using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using XamlVisualEditor.App.Services;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Debugging;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.Language;
using XamlVisualEditor.Shell.ViewModels;
using WorkspaceExtensionEntry = XamlVisualEditor.WorkspaceExtension.WorkspaceExtension;

namespace XamlVisualEditor.Tests.UI;

public sealed class ExtensionCommandMenuIntegrationTests
{
    [AvaloniaFact]
    public async Task Edit_Menu_Commands_Track_Text_Document_State_And_Execute()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"xve-menu-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(filePath, "alpha beta");

        try
        {
            using ExtensionMenuTestHost host = new();
            await host.ActivateAsync();
            await host.OpenFileAsync(filePath);

            ExtensionMenuItemViewModel copyItem = await host.GetMenuItemAsync(host.ViewModel.EditMenuItems, "editing.copy");
            ExtensionMenuItemViewModel pasteItem = await host.GetMenuItemAsync(host.ViewModel.EditMenuItems, "editing.paste");
            ExtensionToolbarItemViewModel copyToolbarItem = await host.GetToolbarItemAsync(host.ViewModel.MainToolbarItems, "editing.copy");
            ExtensionAsyncCommand copyCommand = await host.GetMenuCommandAsync(host.ViewModel.EditMenuItems, "editing.copy");
            ExtensionAsyncCommand pasteCommand = await host.GetMenuCommandAsync(host.ViewModel.EditMenuItems, "editing.paste");
            ExtensionAsyncCommand deleteCommand = await host.GetMenuCommandAsync(host.ViewModel.EditMenuItems, "editing.delete");
            ExtensionAsyncCommand selectAllCommand = await host.GetMenuCommandAsync(host.ViewModel.EditMenuItems, "editing.selectAll");
            ExtensionAsyncCommand undoCommand = await host.GetMenuCommandAsync(host.ViewModel.EditMenuItems, "editing.undo");
            ExtensionAsyncCommand redoCommand = await host.GetMenuCommandAsync(host.ViewModel.EditMenuItems, "editing.redo");

            Assert.False(copyItem.IsEnabled);
            Assert.False(pasteItem.IsEnabled);
            Assert.False(copyToolbarItem.IsEnabled);
            Assert.NotNull(copyItem.InputGesture);
            Assert.NotNull(pasteItem.InputGesture);
            Assert.False(await host.CanExecuteAsync(copyCommand));
            Assert.False(await host.CanExecuteAsync(pasteCommand));
            Assert.False(await host.CanExecuteAsync(redoCommand));
            Assert.True(await host.CanExecuteAsync(selectAllCommand));

            bool copyChanged = false;
            await host.SubscribeCanExecuteChangedAsync(copyCommand, () => copyChanged = true);

            await host.SetTextSelectionAsync(start: 0, length: 5);

            Assert.True(copyChanged);
            Assert.True(copyItem.IsEnabled);
            Assert.True(copyToolbarItem.IsEnabled);
            Assert.True(await host.CanExecuteAsync(copyCommand));
            Assert.True(await host.CanExecuteAsync(deleteCommand));

            await host.ExecuteCommandAsync("editing.copy");
            Assert.True(pasteItem.IsEnabled);
            Assert.True(await host.CanExecuteAsync(pasteCommand));

            await host.ExecuteCommandAsync("editing.cut");
            Assert.Equal(" beta", await host.GetActiveTextAsync());
            Assert.False(copyItem.IsEnabled);
            Assert.False(copyToolbarItem.IsEnabled);
            Assert.True(await host.CanExecuteAsync(undoCommand));
            Assert.False(await host.CanExecuteAsync(copyCommand));

            await host.ExecuteCommandAsync("editing.undo");
            Assert.Equal("alpha beta", await host.GetActiveTextAsync());
            Assert.True(await host.CanExecuteAsync(redoCommand));

            await host.ExecuteCommandAsync("editing.redo");
            Assert.Equal(" beta", await host.GetActiveTextAsync());

            await host.ExecuteCommandAsync("editing.paste");
            Assert.Equal("alpha beta", await host.GetActiveTextAsync());

            await host.ExecuteCommandAsync("editing.selectAll");
            Assert.Equal(
                await host.GetActiveTextLengthAsync(),
                await host.GetSelectionLengthAsync());

            await host.ExecuteCommandAsync("editing.delete");
            Assert.Equal(string.Empty, await host.GetActiveTextAsync());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [AvaloniaFact]
    public async Task Debug_Start_Menu_Command_Follows_Debugger_Service_Availability()
    {
        using ExtensionMenuTestHost host = new();
        await host.ActivateAsync();

        ExtensionMenuItemViewModel startDebugItem = await host.GetMenuItemAsync(host.ViewModel.DebugMenuItems, "debug.start");
        ExtensionAsyncCommand startDebugCommand = await host.GetMenuCommandAsync(host.ViewModel.DebugMenuItems, "debug.start");
        await host.SetWorkspaceLoadedAsync("/tmp/Test.sln");

        Assert.False(startDebugItem.IsEnabled);
        Assert.NotNull(startDebugItem.InputGesture);
        Assert.False(await host.CanExecuteAsync(startDebugCommand));

        bool changed = false;
        await host.SubscribeCanExecuteChangedAsync(startDebugCommand, () => changed = true);
        await host.RegisterDebuggerServiceAsync();

        Assert.True(changed);
        Assert.True(startDebugItem.IsEnabled);
        Assert.True(await host.CanExecuteAsync(startDebugCommand));
    }

    [AvaloniaFact]
    public async Task Workspace_Menu_Commands_Track_Loaded_And_Busy_State()
    {
        using ExtensionMenuTestHost host = new();
        await host.ActivateAsync();

        string[] commandIds =
        {
            "workspace.load",
            "workspace.restore",
            "workspace.build",
            "workspace.rebuild",
            "workspace.clean"
        };

        foreach (string commandId in commandIds)
        {
            ExtensionMenuItemViewModel item = await host.GetMenuItemAsync(host.ViewModel.WorkspaceMenuItems, commandId);
            Assert.False(item.IsEnabled);
        }

        await host.SetWorkspaceLoadedAsync("/tmp/Test.sln");

        foreach (string commandId in commandIds)
        {
            ExtensionMenuItemViewModel item = await host.GetMenuItemAsync(host.ViewModel.WorkspaceMenuItems, commandId);
            Assert.True(item.IsEnabled);
        }

        await host.SetWorkspaceCommandRunningAsync(true);

        foreach (string commandId in commandIds)
        {
            ExtensionMenuItemViewModel item = await host.GetMenuItemAsync(host.ViewModel.WorkspaceMenuItems, commandId);
            Assert.False(item.IsEnabled);
        }
    }

    private sealed class ExtensionMenuTestHost : IDisposable
    {
        private readonly IWorkspaceService _workspaceService = new StubWorkspaceService();
        private readonly WorkspaceInfoService _workspaceInfo = new();
        private readonly CommandRegistry _commands = new();
        private readonly CommandMetadataRegistry _commandMetadata = new();
        private readonly ExtensionContributionRegistry _contributions = new();
        private readonly ExtensionViewRegistry _views = new();
        private readonly DebuggerServiceRegistry _debuggerRegistry = new();
        private readonly InMemoryWorkspace _workspace = new();
        private readonly EditorServicesAdapter _editor;
        private readonly LanguageServiceRegistry _languageRegistry;
        private readonly NavigationHistoryServiceAdapter _navigationHistory;
        private readonly CollaborationPanelHostAdapter _collaborationHost;
        private readonly DebugSettingsHostAdapter _debugSettingsHost;
        private readonly TerminalBridgeAdapter _terminalBridge;
        private readonly DiagnosticsServiceAdapter _diagnostics;
        private readonly ExtensionViewHostAdapter _viewHost;
        private readonly WorkspaceModelAdapter _workspaceModel;
        private readonly BuiltInExtensionHost _extensionHost;

        public ExtensionMenuTestHost()
        {
            _languageRegistry = new LanguageServiceRegistry(Array.Empty<ILanguageIntellisenseService>());
            ViewModel = new MainWindowViewModel(
                workspaceService: _workspaceService,
                workspaceInfoUpdater: _workspaceInfo,
                languageRegistry: _languageRegistry,
                debuggerRegistry: _debuggerRegistry,
                extensionCommands: _commands,
                commandMetadata: _commandMetadata,
                extensionContributionRegistry: _contributions,
                extensionViewRegistry: _views);

            _editor = new EditorServicesAdapter(ViewModel);
            _navigationHistory = new NavigationHistoryServiceAdapter(ViewModel);
            _collaborationHost = new CollaborationPanelHostAdapter(ViewModel);
            _debugSettingsHost = new DebugSettingsHostAdapter(ViewModel);
            _terminalBridge = new TerminalBridgeAdapter(ViewModel);
            _diagnostics = new DiagnosticsServiceAdapter(ViewModel);
            _viewHost = new ExtensionViewHostAdapter(ViewModel);
            _workspaceModel = new WorkspaceModelAdapter(_workspaceInfo, ViewModel, _workspaceService);

            _extensionHost = new BuiltInExtensionHost(
                new IXveExtension[] { new WorkspaceExtensionEntry(ViewModel, _workspaceInfo) },
                _commands,
                _commandMetadata,
                _contributions,
                _debuggerRegistry,
                new DesignerHostAdapter(_editor),
                _workspace,
                _workspaceModel,
                _workspaceInfo,
                new StubSystemIconService(),
                new InMemoryWindow(),
                new InMemoryDialogHost(),
                new InMemoryWorkspaceHost(),
                _views,
                new ExtensionLanguageServiceRegistry(),
                new LanguageNavigationServiceAdapter(_languageRegistry, _editor),
                _navigationHistory,
                new AnimationEditorHostAdapter(ViewModel),
                _collaborationHost,
                _collaborationHost,
                _debugSettingsHost,
                new LspSettingsHostAdapter(null),
                _editor,
                _diagnostics,
                new PropertyEditorRegistry(),
                _terminalBridge,
                _viewHost,
                new InMemorySettingsStore());
        }

        public MainWindowViewModel ViewModel { get; }

        public async Task ActivateAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => _extensionHost.ActivateAsync(CancellationToken.None),
                DispatcherPriority.Background);
            await FlushAsync();
        }

        public async Task OpenFileAsync(string filePath)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => ViewModel.OpenFileAsync(filePath),
                DispatcherPriority.Background);
            await FlushAsync();
        }

        public async Task ExecuteCommandAsync(string commandId)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => _commands.ExecuteAsync(commandId, null, CancellationToken.None),
                DispatcherPriority.Background);
            await FlushAsync();
        }

        public Task<string> GetActiveTextAsync()
        {
            return Dispatcher.UIThread.InvokeAsync(
                () => ViewModel.ActiveTextDocument?.Document.Text ?? string.Empty,
                DispatcherPriority.Background).GetTask();
        }

        public Task<int> GetActiveTextLengthAsync()
        {
            return Dispatcher.UIThread.InvokeAsync(
                () => ViewModel.ActiveTextDocument?.Document.TextLength ?? 0,
                DispatcherPriority.Background).GetTask();
        }

        public Task<int> GetSelectionLengthAsync()
        {
            return Dispatcher.UIThread.InvokeAsync(
                () => ViewModel.ActiveTextDocument?.SelectionLength ?? 0,
                DispatcherPriority.Background).GetTask();
        }

        public Task<ExtensionAsyncCommand> GetMenuCommandAsync(
            System.Collections.ObjectModel.ObservableCollection<ExtensionMenuItemViewModel> items,
            string commandId)
        {
            return Dispatcher.UIThread.InvokeAsync(
                () => Assert.IsType<ExtensionAsyncCommand>(items.Single(item => item.CommandId == commandId).Command),
                DispatcherPriority.Background).GetTask();
        }

        public Task<ExtensionMenuItemViewModel> GetMenuItemAsync(
            System.Collections.ObjectModel.ObservableCollection<ExtensionMenuItemViewModel> items,
            string commandId)
        {
            return Dispatcher.UIThread.InvokeAsync(
                () => items.Single(item => item.CommandId == commandId),
                DispatcherPriority.Background).GetTask();
        }

        public Task<ExtensionToolbarItemViewModel> GetToolbarItemAsync(
            System.Collections.ObjectModel.ObservableCollection<ExtensionToolbarItemViewModel> items,
            string commandId)
        {
            return Dispatcher.UIThread.InvokeAsync(
                () => items.Single(item => item.CommandId == commandId),
                DispatcherPriority.Background).GetTask();
        }

        public Task<bool> CanExecuteAsync(ICommand command)
        {
            return Dispatcher.UIThread.InvokeAsync(
                () => command.CanExecute(null),
                DispatcherPriority.Background).GetTask();
        }

        public async Task SubscribeCanExecuteChangedAsync(ICommand command, Action onChanged)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => command.CanExecuteChanged += (_, _) => onChanged(),
                DispatcherPriority.Background);
        }

        public async Task SetTextSelectionAsync(int start, int length)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TextDocumentViewModel document = Assert.IsType<TextDocumentViewModel>(ViewModel.ActiveTextDocument);
                document.SelectionStart = start;
                document.SelectionLength = length;
                document.SetCaretOffset(start + length);
            }, DispatcherPriority.Background);

            await FlushAsync();
        }

        public async Task SetWorkspaceLoadedAsync(string workspacePath)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _workspaceInfo.UpdateWorkspacePath(workspacePath);
                typeof(MainWindowViewModel)
                    .GetProperty(nameof(MainWindowViewModel.HasWorkspace))!
                    .GetSetMethod(nonPublic: true)!
                    .Invoke(ViewModel, new object[] { true });
            }, DispatcherPriority.Background);

            await FlushAsync();
        }

        public async Task SetWorkspaceCommandRunningAsync(bool value)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                typeof(MainWindowViewModel)
                    .GetProperty(nameof(MainWindowViewModel.IsWorkspaceCommandRunning))!
                    .GetSetMethod(nonPublic: true)!
                    .Invoke(ViewModel, new object[] { value });
            }, DispatcherPriority.Background);

            await FlushAsync();
        }

        public async Task RegisterDebuggerServiceAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _debuggerRegistry.Register(
                    new DebuggerServiceRegistration("fake", "Fake Debugger", new NoOpDebuggerService()),
                    makeDefault: true);
            }, DispatcherPriority.Background);

            await FlushAsync();
        }

        public async Task FlushAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        public void Dispose()
        {
            _extensionHost.Dispose();
            _workspaceModel.Dispose();
            _viewHost.Dispose();
            _diagnostics.Dispose();
            _terminalBridge.Dispose();
            _debugSettingsHost.Dispose();
            _collaborationHost.Dispose();
            _navigationHistory.Dispose();
            _editor.Dispose();
            ViewModel.Dispose();
        }
    }

    private sealed class StubSystemIconService : ISystemIconService
    {
        public object? GetIcon(string? path, bool isDirectory, object? fallbackIcon = null, int iconSize = 16)
            => fallbackIcon;

        public object? GetFileIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
            => fallbackIcon;

        public object? GetFolderIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
            => fallbackIcon;
    }

    private sealed class StubWorkspaceService : IWorkspaceService
    {
        public Task<WorkspaceModel> LoadSolutionAsync(string solutionPath, CancellationToken ct = default) =>
            Task.FromResult(new WorkspaceModel { Projects = Array.Empty<ProjectModel>() });

        public Task<WorkspaceModel> LoadProjectAsync(string projectPath, CancellationToken ct = default) =>
            Task.FromResult(new WorkspaceModel { Projects = Array.Empty<ProjectModel>() });

        public WorkspaceModel CreateStandaloneWorkspace(string xamlFilePath) =>
            new() { Projects = Array.Empty<ProjectModel>() };
    }

    private sealed class NoOpDebuggerService : IDebuggerService
    {
        public Task<IDebugSession> LaunchAsync(DebugLaunchOptions options, CancellationToken ct = default) =>
            Task.FromResult<IDebugSession>(new NoOpDebugSession());

        public Task<IDebugSession> AttachAsync(DebugAttachOptions options, CancellationToken ct = default) =>
            Task.FromResult<IDebugSession>(new NoOpDebugSession());
    }

    private sealed class NoOpDebugSession : IDebugSession
    {
        public DebugSessionState State => DebugSessionState.Created;

#pragma warning disable CS0067
        public event Action<DebugSessionState>? StateChanged;
        public event Action<DebugEvent>? EventReceived;
#pragma warning restore CS0067

        public Task<IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(
            string filePath,
            IReadOnlyList<SourceBreakpoint> breakpoints,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BreakpointInfo>>(Array.Empty<BreakpointInfo>());

        public Task<IReadOnlyList<ThreadInfo>> GetThreadsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ThreadInfo>>(Array.Empty<ThreadInfo>());

        public Task<StackTraceInfo> GetStackTraceAsync(int threadId, int startFrame, int levels, CancellationToken ct = default) =>
            Task.FromResult(new StackTraceInfo { Frames = Array.Empty<StackFrameInfo>() });

        public Task<IReadOnlyList<ScopeInfo>> GetScopesAsync(int frameId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ScopeInfo>>(Array.Empty<ScopeInfo>());

        public Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(int variablesReference, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<VariableInfo>>(Array.Empty<VariableInfo>());

        public Task<EvaluateResult> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default) =>
            Task.FromResult(new EvaluateResult { Result = string.Empty });

        public Task ContinueAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepInAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepOutAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepOverAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task PauseAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(bool terminateDebuggee, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
