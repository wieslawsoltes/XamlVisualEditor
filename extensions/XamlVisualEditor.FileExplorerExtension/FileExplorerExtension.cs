using System.Reactive.Disposables;
using System.Threading;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.FileExplorerExtension.ViewModels;
using XamlVisualEditor.FileExplorerExtension.Views;

namespace XamlVisualEditor.FileExplorerExtension;

public sealed class FileExplorerExtension : IXveExtension
{
    private const string ViewId = "fileExplorer.panel";
    private const string OpenFolderDialogId = "fileExplorer.openFolderDialog";
    private const string OpenFolderCommandId = "fileExplorer.openFolder";
    private const string OpenFolderIconPath =
        "M3 5.5v6.6l1.5-2.6A3 3 0 0 1 7.1 8H15v-.5c0-.83-.67-1.5-1.5-1.5h-4a.5.5 0 0 1-.35-.15l-1.71-1.7A.5.5 0 0 0 7.09 4H4.5C3.67 4 3 4.67 3 5.5Zm1.28 10.48.22.02h9.4a2 2 0 0 0 1.73-1l2.17-3.75A1.5 1.5 0 0 0 16.5 9H7.1a2 2 0 0 0-1.73 1L3.2 13.75a1.5 1.5 0 0 0 1.08 2.23ZM2 14.46V5.5A2.5 2.5 0 0 1 4.5 3h2.59c.4 0 .78.16 1.06.44L9.7 5h3.79A2.5 2.5 0 0 1 16 7.5V8h.5a2.5 2.5 0 0 1 2.16 3.75L16.5 15.5a3 3 0 0 1-2.6 1.5H4.5a2.54 2.54 0 0 1-1.62-.6A2.5 2.5 0 0 1 2 14.46Z";
    private const string ToggleHiddenCommandId = "fileExplorer.toggleHidden";
    private const string SetIconProviderCommandId = "fileExplorer.setIconProvider";
    private const string SetViewLocationCommandId = "fileExplorer.setViewLocation";
    private const string ToggleViewCommandId = "fileExplorer.toggleView";
    private const string ShowHiddenKey = "fileExplorer.showHidden";
    private const string IconProviderKey = "fileExplorer.iconProvider";
    private const string IconSizeKey = "fileExplorer.iconSize";
    private const string ViewLocationKey = "fileExplorer.viewLocation";
    private const string UseOpenFolderDialogKey = "fileExplorer.useOpenFolderDialog";

    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IEditorServices _editor;
    private readonly IFolderPicker _folderPicker;
    private readonly ISettings _settings;
    private IWindow? _window;
    private IWorkspaceHost? _workspaceHost;
    private FileExplorerTreeDataProvider? _provider;
    private IExtensionContributionRegistry? _contributions;
    private string? _extensionId;
    private IDisposable? _viewRegistration;

    public FileExplorerExtension(
        IWorkspaceInfo workspaceInfo,
        IEditorServices editor,
        IFolderPicker folderPicker,
        ISettings settings)
    {
        _workspaceInfo = workspaceInfo;
        _editor = editor;
        _folderPicker = folderPicker;
        _settings = settings;
    }

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        _window = context.Window;
        _workspaceHost = context.WorkspaceHost;
        _contributions = context.Contributions;
        _extensionId = context.ExtensionId;
        context.Subscriptions.Add(context.DialogHost.RegisterDialog(
            OpenFolderDialogId,
            viewModel => new OpenFolderDialog { DataContext = viewModel }));

        RegisterViewContribution();

        _provider = CreateProvider();
        IDisposable providerRegistration = context.Views.RegisterTreeDataProvider(
            ViewId,
            _provider);
        context.Subscriptions.Add(providerRegistration);
        context.Subscriptions.Add(Disposable.Create(() => _provider.Dispose()));

        context.Subscriptions.Add(context.Commands.Register(OpenFolderCommandId, _ =>
            OpenFolderAsync(context)));
        context.Subscriptions.Add(context.Commands.Register(ToggleHiddenCommandId, _ =>
            ToggleHiddenFilesAsync(context)));
        context.Subscriptions.Add(context.Commands.Register(SetIconProviderCommandId, _ =>
            ConfigureIconProviderAsync(context)));
        context.Subscriptions.Add(context.Commands.Register(SetViewLocationCommandId, _ =>
            ConfigureViewLocationAsync(context)));
        context.Subscriptions.Add(context.Commands.Register(ToggleViewCommandId, _ =>
            context.ViewHost.ToggleAsync(ViewId, CancellationToken.None)));

        ExtensionMenuContribution[] menuItems =
        {
            new(OpenFolderCommandId, "Open Folder...", ExtensionMenuLocations.File, "workspace", 5),
            new(ToggleViewCommandId, "File Explorer", ExtensionMenuLocations.View, "views.left", 5),
            new(ToggleHiddenCommandId, "Toggle Hidden Files", ExtensionMenuLocations.ToolsWorkspace, "fileExplorer", 10),
            new(SetIconProviderCommandId, "File Explorer Icons...", ExtensionMenuLocations.ToolsWorkspace, "fileExplorer", 20),
            new(SetViewLocationCommandId, "File Explorer Dock...", ExtensionMenuLocations.ToolsWorkspace, "fileExplorer", 30)
        };
        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(context.ExtensionId, menuItems));

        ExtensionToolbarContribution[] toolbarItems =
        {
            new(
                OpenFolderCommandId,
                "Open Folder",
                "Open a folder",
                ExtensionToolbarLocations.Main,
                "workspace",
                5,
                OpenFolderIconPath)
        };
        context.Subscriptions.Add(context.Contributions.RegisterToolbarItems(context.ExtensionId, toolbarItems));

        ExtensionCommandPaletteContribution[] paletteItems =
        {
            new(OpenFolderCommandId, "Open Folder...", "File"),
            new(ToggleHiddenCommandId, "Toggle Hidden Files", "View"),
            new(SetIconProviderCommandId, "File Explorer Icons...", "View"),
            new(SetViewLocationCommandId, "File Explorer Dock...", "View")
        };
        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(context.ExtensionId, paletteItems));

        return Task.CompletedTask;
    }

    private FileExplorerTreeDataProvider CreateProvider()
    {
        bool showHidden = _settings.Get(ShowHiddenKey, true);
        FileExplorerIconProviderKind kind = ParseIconProvider(_settings.Get<string>(IconProviderKey));
        int iconSize = NormalizeIconSize(_settings.Get(IconSizeKey, 16));
        IFileExplorerIconProvider iconProvider = FileExplorerIconProviderFactory.Create(kind, iconSize);
        IWindow window = _window ?? throw new InvalidOperationException("Window services not available.");
        IWorkspaceHost workspaceHost = _workspaceHost ?? throw new InvalidOperationException("Workspace host not available.");
        return new FileExplorerTreeDataProvider(_workspaceInfo, _editor, window, workspaceHost, iconProvider, showHidden);
    }

    private async Task OpenFolderAsync(ExtensionContext context)
    {
        string? path;
        bool useDialog = _settings.Get(UseOpenFolderDialogKey, false);

        if (useDialog)
        {
            OpenFolderDialogViewModel viewModel = new();
            path = await context.DialogHost.ShowDialogAsync<string?>(
                OpenFolderDialogId,
                viewModel,
                CancellationToken.None);
        }
        else
        {
            path = await _folderPicker.PickFolderAsync("Open Folder", CancellationToken.None);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        QuickPickItem[] options =
        {
            new("Open in Current Window", "Replace the current workspace", null),
            new("Open in New Window", "Open the folder in a new window", null)
        };

        QuickPickItem? choice = await context.Window.ShowQuickPickAsync(
            options,
            new QuickPickOptions("Open Folder", CanPickMany: false),
            CancellationToken.None);

        WorkspaceOpenMode mode = choice is not null
            && choice.Label.StartsWith("Open in New", StringComparison.OrdinalIgnoreCase)
                ? WorkspaceOpenMode.NewWindow
                : WorkspaceOpenMode.CurrentWindow;

        await context.WorkspaceHost.OpenWorkspaceAsync(path, mode, CancellationToken.None);
    }

    private Task ToggleHiddenFilesAsync(ExtensionContext context)
    {
        bool showHidden = !_settings.Get(ShowHiddenKey, true);
        _ = _settings.UpdateAsync(ShowHiddenKey, showHidden, SettingsTarget.User, CancellationToken.None);

        UpdateProviderSettings();
        return context.Window.ShowInformationMessageAsync(
            showHidden ? "Hidden files are now visible." : "Hidden files are now hidden.",
            CancellationToken.None);
    }

    private async Task ConfigureIconProviderAsync(ExtensionContext context)
    {
        QuickPickItem[] options =
        {
            new("Native", "Use OS-specific icon provider", null),
            new("Theme", "Use bundled icon theme", null)
        };

        QuickPickItem? choice = await context.Window.ShowQuickPickAsync(
            options,
            new QuickPickOptions("File Explorer Icons", CanPickMany: false),
            CancellationToken.None);

        if (choice is null)
        {
            return;
        }

        string selected = choice.Label.Equals("Theme", StringComparison.OrdinalIgnoreCase)
            ? "theme"
            : "native";

        await _settings.UpdateAsync(IconProviderKey, selected, SettingsTarget.User, CancellationToken.None);
        UpdateProviderSettings();
    }

    private void UpdateProviderSettings()
    {
        if (_provider is null)
        {
            return;
        }

        bool showHidden = _settings.Get(ShowHiddenKey, true);
        FileExplorerIconProviderKind kind = ParseIconProvider(_settings.Get<string>(IconProviderKey));
        int iconSize = NormalizeIconSize(_settings.Get(IconSizeKey, 16));
        IFileExplorerIconProvider iconProvider = FileExplorerIconProviderFactory.Create(kind, iconSize);
        _provider.UpdateSettings(iconProvider, showHidden);
    }

    private static FileExplorerIconProviderKind ParseIconProvider(string? value)
    {
        return string.Equals(value, "theme", StringComparison.OrdinalIgnoreCase)
            ? FileExplorerIconProviderKind.Theme
            : FileExplorerIconProviderKind.Native;
    }

    private static int NormalizeIconSize(int value)
    {
        if (value < 8)
        {
            return 16;
        }

        if (value > 128)
        {
            return 128;
        }

        return value;
    }

    private ExtensionViewLocation ResolveViewLocation()
    {
        string? value = _settings.Get<string>(ViewLocationKey);
        if (string.Equals(value, "right", StringComparison.OrdinalIgnoreCase))
        {
            return ExtensionViewLocation.Right;
        }

        if (string.Equals(value, "bottom", StringComparison.OrdinalIgnoreCase))
        {
            return ExtensionViewLocation.Bottom;
        }

        return ExtensionViewLocation.Left;
    }

    private void RegisterViewContribution()
    {
        if (_contributions is null || string.IsNullOrWhiteSpace(_extensionId))
        {
            return;
        }

        _viewRegistration?.Dispose();
        ExtensionViewContribution view = new(
            ViewId,
            "File Explorer",
            ExtensionViewType.Tree,
            ResolveViewLocation(),
            15,
            ActivateByDefault: true);

        _viewRegistration = _contributions.RegisterViews(_extensionId, new[] { view });
    }

    private async Task ConfigureViewLocationAsync(ExtensionContext context)
    {
        QuickPickItem[] options =
        {
            new("Left", "Dock in the left tool panel", null),
            new("Right", "Dock in the right tool panel", null),
            new("Bottom", "Dock in the bottom panel", null)
        };

        QuickPickItem? choice = await context.Window.ShowQuickPickAsync(
            options,
            new QuickPickOptions("File Explorer Dock", CanPickMany: false),
            CancellationToken.None);

        if (choice is null)
        {
            return;
        }

        string selected = choice.Label.ToLowerInvariant();
        await _settings.UpdateAsync(ViewLocationKey, selected, SettingsTarget.User, CancellationToken.None);
        RegisterViewContribution();
    }
}
