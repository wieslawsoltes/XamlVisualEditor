using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.Mcp;

namespace XamlVisualEditor.McpExtension;

public sealed partial class McpPermissionEntryViewModel
{
    public McpPermissionEntryViewModel(McpWorkspacePermissionEntry entry)
    {
        WorkspaceId = entry.WorkspaceId;
        SessionToken = entry.SessionToken;
        GrantedAt = entry.GrantedAt.ToLocalTime().ToString("u");
        AccessLevel = entry.AccessLevel.ToString();
    }

    public string WorkspaceId { get; }

    public string SessionToken { get; }

    public string GrantedAt { get; }

    public string AccessLevel { get; }
}

public sealed partial class McpPanelViewModel : ReactiveObject, IDisposable
{
    private readonly McpRuntimeController _controller;
    private readonly McpPermissionService _permissions;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly CompositeDisposable _disposables = new();
    private readonly ObservableCollection<McpPermissionEntryViewModel> _permissionEntries = new();
    private int _lastConnectionCount;

    public Interaction<string, Unit> CopyToClipboardInteraction { get; } = new();

    [Reactive]
    public partial bool IsEnabled { get; set; }

    [Reactive]
    public partial string SelectedTransport { get; set; } = "both";

    [Reactive]
    public partial double HttpPort { get; set; } = 4712;

    [Reactive]
    public partial string? HttpPath { get; set; }

    [Reactive]
    public partial string StatusText { get; private set; } = "Stopped";

    [Reactive]
    public partial string EndpointText { get; private set; } = "stdio + http";

    [Reactive]
    public partial int ConnectionCount { get; private set; }

    [Reactive]
    public partial string? LastConnectionAt { get; private set; }

    [Reactive]
    public partial string? WorkspaceId { get; private set; }

    [Reactive]
    public partial string? SessionToken { get; private set; }

    [Reactive]
    public partial McpPermissionEntryViewModel? SelectedPermission { get; set; }

    public IReadOnlyList<string> TransportOptions { get; } = new[] { "stdio", "http", "both" };

    public IReadOnlyList<McpPermissionEntryViewModel> Permissions => _permissionEntries;

    public ObservableCollection<string> ActivityLog { get; } = new();

    public ReactiveCommand<Unit, Unit> ApplySettingsCommand { get; }

    public ReactiveCommand<Unit, Unit> RestartCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshPermissionsCommand { get; }

    public ReactiveCommand<Unit, Unit> RevokePermissionCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearPermissionsCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyTokenCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyHttpEndpointCommand { get; }

    public McpPanelViewModel(
        McpRuntimeController controller,
        McpPermissionService permissions,
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
        CopyHttpEndpointCommand = ReactiveCommand.CreateFromTask(CopyHttpEndpointAsync, this.WhenAnyValue(x => x.SelectedTransport).Select(transport =>
            string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transport, "both", StringComparison.OrdinalIgnoreCase)));

        McpSettings settings = _controller.CurrentSettings;
        IsEnabled = settings.Enabled;
        SelectedTransport = settings.Transport ?? "both";
        HttpPort = settings.HttpPort;
        HttpPath = settings.HttpPath;

        UpdateStatus();
        UpdateEndpointPreview();
        UpdateWorkspaceId();

        _workspaceInfo.WorkspaceChanged += OnWorkspaceChanged;
        _disposables.Add(Disposable.Create(() => _workspaceInfo.WorkspaceChanged -= OnWorkspaceChanged));
        _controller.StatusChanged += OnStatusChanged;
        _disposables.Add(Disposable.Create(() => _controller.StatusChanged -= OnStatusChanged));
        _disposables.Add(this.WhenAnyValue(x => x.SelectedTransport, x => x.HttpPort, x => x.HttpPath)
            .Subscribe(_ => UpdateEndpointPreview()));

        _ = RefreshPermissionsAsync();
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private async Task ApplySettingsAsync()
    {
        McpSettings settings = BuildSettings();
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
        IReadOnlyList<McpWorkspacePermissionEntry> entries = await _permissions.GetPermissionsAsync(CancellationToken.None).ConfigureAwait(false);
        _permissionEntries.Clear();
        foreach (McpWorkspacePermissionEntry entry in entries.OrderBy(e => e.WorkspaceId, StringComparer.OrdinalIgnoreCase))
        {
            _permissionEntries.Add(new McpPermissionEntryViewModel(entry));
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

    private async Task CopyHttpEndpointAsync()
    {
        string endpoint = BuildHttpEndpoint(BuildSettings());
        await CopyToClipboardInteraction.Handle(endpoint).ToTask().ConfigureAwait(false);
        AppendLog("Copied HTTP endpoint.");
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

    private void UpdateEndpointPreview()
    {
        EndpointText = McpRuntimeController.BuildEndpointPreview(BuildSettings());
    }

    private void UpdateWorkspaceId()
    {
        string? path = _workspaceInfo.WorkspacePath;
        WorkspaceId = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    }

    private void UpdateSessionToken()
    {
        if (string.IsNullOrWhiteSpace(WorkspaceId))
        {
            SessionToken = null;
            return;
        }

        McpPermissionEntryViewModel? match = _permissionEntries.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceId, WorkspaceId, StringComparison.OrdinalIgnoreCase));

        SessionToken = match?.SessionToken;
    }

    private McpSettings BuildSettings()
    {
        return new McpSettings(
            Enabled: IsEnabled,
            Transport: SelectedTransport,
            HttpPort: (int)HttpPort,
            HttpPath: string.IsNullOrWhiteSpace(HttpPath) ? "/mcp/" : HttpPath);
    }

    private static string BuildHttpEndpoint(McpSettings settings)
    {
        string path = string.IsNullOrWhiteSpace(settings.HttpPath) ? "/mcp/" : settings.HttpPath;
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }
        if (!path.EndsWith("/", StringComparison.Ordinal))
        {
            path += "/";
        }

        return $"http://127.0.0.1:{(settings.HttpPort > 0 ? settings.HttpPort : 4712)}{path}";
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        UpdateWorkspaceId();
        UpdateSessionToken();
    }

    private void OnStatusChanged(object? sender, EventArgs args)
    {
        UpdateStatus();
    }

    private void AppendLog(string message)
    {
        ActivityLog.Add(DateTime.Now.ToString("HH:mm:ss") + " " + message);
    }
}
