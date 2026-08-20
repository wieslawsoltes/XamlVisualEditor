using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;

namespace XamlVisualEditor.IdeBridgeExtension;

public sealed partial class IdeBridgePermissionEntryViewModel
{
    public IdeBridgePermissionEntryViewModel(IdeBridgeWorkspacePermissionEntry entry)
    {
        WorkspaceId = entry.WorkspaceId;
        SessionToken = entry.SessionToken;
        GrantedAt = entry.GrantedAt.ToLocalTime().ToString("u");
        Capabilities = FormatCapabilities(entry.Capabilities);
    }

    public string WorkspaceId { get; }

    public string SessionToken { get; }

    public string GrantedAt { get; }

    public string Capabilities { get; }

    private static string FormatCapabilities(IdeBridgeCapabilities caps)
    {
        List<string> parts = new();
        if (caps.Files)
        {
            parts.Add("files");
        }

        if (caps.Documents)
        {
            parts.Add("docs");
        }

        if (caps.Selection)
        {
            parts.Add("selection");
        }

        if (caps.Commands)
        {
            parts.Add("commands");
        }

        if (caps.Diagnostics)
        {
            parts.Add("diagnostics");
        }

        if (caps.Terminal)
        {
            parts.Add("terminal");
        }

        if (caps.Ui)
        {
            parts.Add("ui");
        }

        if (caps.Workspace)
        {
            parts.Add("workspace");
        }

        if (caps.Write)
        {
            parts.Add("write");
        }

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }
}

public sealed partial class IdeBridgePanelViewModel : ReactiveObject, IDisposable
{
    private readonly IdeBridgeRuntimeController _controller;
    private readonly IdeBridgePermissionService _permissions;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly CompositeDisposable _disposables = new();
    private readonly ObservableCollection<IdeBridgePermissionEntryViewModel> _permissionEntries = new();
    private int _lastConnectionCount;

    public Interaction<string, Unit> CopyToClipboardInteraction { get; } = new();

    [Reactive]
    public partial bool IsEnabled { get; set; }

    [Reactive]
    public partial string SelectedTransport { get; set; } = "stdio";

    [Reactive]
    public partial double TcpPort { get; set; } = 4711;

    [Reactive]
    public partial string? UnixSocketPath { get; set; }

    [Reactive]
    public partial string StatusText { get; private set; } = "Stopped";

    [Reactive]
    public partial string EndpointText { get; private set; } = "stdio";

    [Reactive]
    public partial int ConnectionCount { get; private set; }

    [Reactive]
    public partial string? LastConnectionAt { get; private set; }

    [Reactive]
    public partial string? WorkspaceId { get; private set; }

    [Reactive]
    public partial string? SessionToken { get; private set; }

    [Reactive]
    public partial IdeBridgePermissionEntryViewModel? SelectedPermission { get; set; }

    public IReadOnlyList<string> TransportOptions { get; } = new[] { "stdio", "tcp", "unix" };

    public IReadOnlyList<IdeBridgePermissionEntryViewModel> Permissions => _permissionEntries;

    public ObservableCollection<string> ActivityLog { get; } = new();

    public ReactiveCommand<Unit, Unit> ApplySettingsCommand { get; }

    public ReactiveCommand<Unit, Unit> RestartCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshPermissionsCommand { get; }

    public ReactiveCommand<Unit, Unit> RevokePermissionCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearPermissionsCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyTokenCommand { get; }

    public IdeBridgePanelViewModel(
        IdeBridgeRuntimeController controller,
        IdeBridgePermissionService permissions,
        IWorkspaceInfo workspaceInfo)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _workspaceInfo = workspaceInfo ?? throw new ArgumentNullException(nameof(workspaceInfo));

        ApplySettingsCommand = ReactiveCommand.CreateFromTask(ApplySettingsAsync);
        RestartCommand = ReactiveCommand.CreateFromTask(RestartAsync);
        RefreshPermissionsCommand = ReactiveCommand.CreateFromTask(RefreshPermissionsAsync);
        RevokePermissionCommand = ReactiveCommand.CreateFromTask(RevokeSelectedAsync, this.WhenAnyValue(x => x.SelectedPermission).Select(p => p is not null));
        ClearPermissionsCommand = ReactiveCommand.CreateFromTask(ClearPermissionsAsync);
        CopyTokenCommand = ReactiveCommand.CreateFromTask(CopyTokenAsync, this.WhenAnyValue(x => x.SessionToken).Select(token => !string.IsNullOrWhiteSpace(token)));

        IdeBridgeSettings settings = _controller.CurrentSettings;
        IsEnabled = settings.Enabled;
        SelectedTransport = settings.Transport ?? "stdio";
        TcpPort = settings.TcpPort;
        UnixSocketPath = settings.UnixSocketPath;

        UpdateStatus();
        UpdateEndpointPreview();
        UpdateWorkspaceId();

        _workspaceInfo.WorkspaceChanged += OnWorkspaceChanged;
        _disposables.Add(Disposable.Create(() => _workspaceInfo.WorkspaceChanged -= OnWorkspaceChanged));
        _controller.StatusChanged += OnStatusChanged;
        _disposables.Add(Disposable.Create(() => _controller.StatusChanged -= OnStatusChanged));
        _disposables.Add(this.WhenAnyValue(x => x.SelectedTransport, x => x.TcpPort, x => x.UnixSocketPath)
            .Subscribe(_ => UpdateEndpointPreview()));

        _ = RefreshPermissionsAsync();
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private async Task ApplySettingsAsync()
    {
        IdeBridgeSettings settings = BuildSettings();
        await _controller.ApplySettingsAsync(settings, CancellationToken.None).ConfigureAwait(false);
        AppendLog("Applied settings.");
        UpdateStatus();
    }

    private async Task RestartAsync()
    {
        await _controller.RestartAsync(CancellationToken.None).ConfigureAwait(false);
        AppendLog("Restarted server.");
        UpdateStatus();
    }

    private async Task RefreshPermissionsAsync()
    {
        IReadOnlyList<IdeBridgeWorkspacePermissionEntry> entries = await _permissions.GetPermissionsAsync(CancellationToken.None).ConfigureAwait(false);
        _permissionEntries.Clear();
        foreach (IdeBridgeWorkspacePermissionEntry entry in entries.OrderBy(e => e.WorkspaceId, StringComparer.OrdinalIgnoreCase))
        {
            _permissionEntries.Add(new IdeBridgePermissionEntryViewModel(entry));
        }

        UpdateSessionToken();
    }

    private async Task RevokeSelectedAsync()
    {
        if (SelectedPermission is null)
        {
            return;
        }

        await _permissions.ClearPermissionAsync(SelectedPermission.WorkspaceId, CancellationToken.None).ConfigureAwait(false);
        AppendLog("Revoked permission for " + SelectedPermission.WorkspaceId + ".");
        await RefreshPermissionsAsync().ConfigureAwait(false);
    }

    private async Task ClearPermissionsAsync()
    {
        await _permissions.ClearAllAsync(CancellationToken.None).ConfigureAwait(false);
        AppendLog("Cleared all permissions.");
        await RefreshPermissionsAsync().ConfigureAwait(false);
    }

    private async Task CopyTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionToken))
        {
            return;
        }

        await CopyToClipboardInteraction.Handle(SessionToken).ToTask().ConfigureAwait(false);
        AppendLog("Copied session token.");
    }

    private void UpdateStatus()
    {
        StatusText = _controller.IsRunning ? "Running" : "Stopped";
        EndpointText = _controller.EndpointSummary;
        ConnectionCount = _controller.ConnectionCount;
        LastConnectionAt = _controller.LastConnectionAt?.ToLocalTime().ToString("u");

        if (ConnectionCount != _lastConnectionCount)
        {
            AppendLog($"Connections: {ConnectionCount}");
            _lastConnectionCount = ConnectionCount;
        }
    }

    private void UpdateWorkspaceId()
    {
        WorkspaceId = NormalizeWorkspaceId(_workspaceInfo.WorkspacePath);
        UpdateSessionToken();
    }

    private void UpdateSessionToken()
    {
        if (string.IsNullOrWhiteSpace(WorkspaceId))
        {
            SessionToken = null;
            return;
        }

        IdeBridgePermissionEntryViewModel? entry = _permissionEntries.FirstOrDefault(p => string.Equals(p.WorkspaceId, WorkspaceId, StringComparison.OrdinalIgnoreCase));
        SessionToken = entry?.SessionToken;
    }

    private void UpdateEndpointPreview()
    {
        IdeBridgeSettings settings = BuildSettings();
        EndpointText = IdeBridgeRuntimeController.BuildEndpointPreview(settings);
    }

    private IdeBridgeSettings BuildSettings()
    {
        string transport = string.IsNullOrWhiteSpace(SelectedTransport) ? "stdio" : SelectedTransport;
        int port = (int)Math.Round(TcpPort);
        return new IdeBridgeSettings
        {
            Enabled = IsEnabled,
            Transport = transport,
            TcpPort = port <= 0 ? 4711 : port,
            UnixSocketPath = string.IsNullOrWhiteSpace(UnixSocketPath) ? null : UnixSocketPath
        };
    }

    private void AppendLog(string message)
    {
        ActivityLog.Insert(0, DateTimeOffset.Now.ToLocalTime().ToString("HH:mm:ss") + " " + message);
        while (ActivityLog.Count > 200)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        WorkspaceId = NormalizeWorkspaceId(e.WorkspacePath);
        UpdateSessionToken();
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        IdeBridgeSettings settings = _controller.CurrentSettings;
        IsEnabled = settings.Enabled;
        SelectedTransport = settings.Transport ?? "stdio";
        TcpPort = settings.TcpPort;
        UnixSocketPath = settings.UnixSocketPath;
        UpdateStatus();
        UpdateEndpointPreview();
    }

    private static string? NormalizeWorkspaceId(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return System.IO.Path.GetFullPath(path);
    }
}
