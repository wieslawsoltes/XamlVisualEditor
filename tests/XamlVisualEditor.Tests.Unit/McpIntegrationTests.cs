using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.Extensions.Hosting.Mcp;
using XamlVisualEditor.McpExtension;
using Xunit;

#pragma warning disable CS0067

namespace XamlVisualEditor.Tests.Unit;

public sealed class McpIntegrationTests
{
    [Fact]
    public async Task InitializeStoresSessionToken()
    {
        InMemorySettingsStore settings = new();
        string workspacePath = "/tmp/xve";
        string workspaceId = System.IO.Path.GetFullPath(workspacePath);
        var allowlist = new Dictionary<string, McpWorkspacePermissionState>
        {
            [workspaceId] = new McpWorkspacePermissionState("token-123", McpAccessLevel.Full, DateTimeOffset.UtcNow)
        };
        await settings.UpdateAsync("mcp.permissions", allowlist, SettingsTarget.Workspace, CancellationToken.None);

        StubWorkspaceInfo workspaceInfo = new(workspacePath);
        McpPermissionService permissionService = new(settings, new InMemoryWindow());
        McpSessionService sessionService = new(permissionService, workspaceInfo);
        McpSessionRegistry registry = new();
        McpHandshakeHandler handler = new(sessionService, registry);
        McpRequestRouter router = new(registry);
        handler.Register(router);

        McpInitializeParams init = new("token-123", null, new McpClientInfo("tests", "1"), null);
        JsonElement parameters = JsonSerializer.SerializeToElement(init, McpMessageFraming.SerializerOptions);

        object? result = await router.DispatchAsync(McpProtocol.InitializeMethod, new McpRequestContext(null, null), parameters, CancellationToken.None);
        McpInitializeResult initResult = Assert.IsType<McpInitializeResult>(result);
        Assert.Equal("token-123", initResult.SessionToken);

        McpSessionInfo? session = registry.Resolve(new McpRequestContext(null, "token-123"));
        Assert.NotNull(session);
        Assert.Equal(McpAccessLevel.Full, session!.AccessLevel);
    }

    [Fact]
    public async Task ToolsCallHonorsWritePermission()
    {
        StubCommands commands = new();
        InMemoryWorkspace workspace = new();
        StubWorkspaceInfo workspaceInfo = new("/tmp/xve");
        StubWindow window = new();
        StubEditorServices editor = new();
        StubDiagnostics diagnostics = new();
        StubTerminalBridge terminal = new();
        InMemorySettingsStore settings = new();

        McpToolCatalog catalog = new(commands, workspace, workspaceInfo, window, editor, diagnostics, terminal, settings);
        McpToolsHandler tools = new(catalog);
        McpSessionRegistry registry = new();
        McpRequestRouter router = new(registry);
        tools.Register(router);

        registry.Set("token-read", new McpSessionInfo("/tmp/xve", McpAccessLevel.ReadOnly, "token-read"));

        McpToolCallParams call = new("xve.workspace.writeFile", JsonSerializer.SerializeToElement(new { path = "test.txt", content = "hi" }));
        JsonElement parameters = JsonSerializer.SerializeToElement(call, McpMessageFraming.SerializerOptions);

        await Assert.ThrowsAsync<McpJsonRpcException>(() =>
            router.DispatchAsync(McpProtocol.ToolsCallMethod, new McpRequestContext(null, "token-read"), parameters, CancellationToken.None));
    }

    [Fact]
    public async Task RuntimeControllerKeepsHttpServerAliveAfterStartupTokenCancellation()
    {
        InMemorySettingsStore settings = new();
        int port = GetAvailablePort();
        await settings.UpdateAsync("mcp", new McpSettings(true, "http", port, "/mcp/"), SettingsTarget.User, CancellationToken.None);

        StubWorkspaceInfo workspaceInfo = new("/tmp/xve");
        StubWindow window = new();
        StubCommands commands = new();
        InMemoryWorkspace workspace = new();
        StubEditorServices editor = new();
        StubDiagnostics diagnostics = new();
        StubTerminalBridge terminal = new();
        McpPermissionService permissions = new(settings, window);

        await using McpRuntimeController controller = new(
            settings,
            permissions,
            commands,
            workspace,
            workspaceInfo,
            window,
            editor,
            diagnostics,
            terminal);

        using CancellationTokenSource startupToken = new();
        await controller.InitializeAsync(startupToken.Token);
        startupToken.Cancel();

        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(2) };
        HttpResponseMessage response = await client.GetAsync($"http://127.0.0.1:{port}/mcp/");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task InitializeReauthorizesWhenSessionTokenIsMissing()
    {
        InMemorySettingsStore settings = new();
        string workspacePath = "/tmp/xve";
        string workspaceId = System.IO.Path.GetFullPath(workspacePath);
        var allowlist = new Dictionary<string, McpWorkspacePermissionState>
        {
            [workspaceId] = new McpWorkspacePermissionState("token-old", McpAccessLevel.ReadOnly, DateTimeOffset.UtcNow)
        };
        await settings.UpdateAsync("mcp.permissions", allowlist, SettingsTarget.Workspace, CancellationToken.None);

        StubWorkspaceInfo workspaceInfo = new(workspacePath);
        StubWindow window = new("Allow full access");
        McpPermissionService permissionService = new(settings, window);
        McpSessionService sessionService = new(permissionService, workspaceInfo);

        McpInitializeOutcome outcome = await sessionService.InitializeAsync(
            new McpInitializeParams(null, null, new McpClientInfo("tests", "1"), null),
            CancellationToken.None);

        Assert.Equal(McpAccessLevel.Full, outcome.AccessLevel);
        Assert.NotEqual("token-old", outcome.Result.SessionToken);

        Dictionary<string, McpWorkspacePermissionState>? persisted = settings.Get<Dictionary<string, McpWorkspacePermissionState>>("mcp.permissions");
        Assert.NotNull(persisted);
        Assert.True(persisted!.TryGetValue(workspaceId, out McpWorkspacePermissionState? updated));
        Assert.NotNull(updated);
        Assert.Equal(outcome.Result.SessionToken, updated!.SessionToken);
    }

    [Fact]
    public async Task InitializeAutoGrantsWhenWindowIsNonInteractive()
    {
        InMemorySettingsStore settings = new();
        string workspacePath = "/tmp/xve";
        StubWorkspaceInfo workspaceInfo = new(workspacePath);
        McpPermissionService permissionService = new(settings, new InMemoryWindow());
        McpSessionService sessionService = new(permissionService, workspaceInfo);

        McpInitializeOutcome outcome = await sessionService.InitializeAsync(
            new McpInitializeParams(null, null, new McpClientInfo("tests", "1"), null),
            CancellationToken.None);

        Assert.Equal(McpAccessLevel.Full, outcome.AccessLevel);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Result.SessionToken));

        string workspaceId = System.IO.Path.GetFullPath(workspacePath);
        Dictionary<string, McpWorkspacePermissionState>? persisted = settings.Get<Dictionary<string, McpWorkspacePermissionState>>("mcp.permissions");
        Assert.NotNull(persisted);
        Assert.True(persisted!.TryGetValue(workspaceId, out McpWorkspacePermissionState? state));
        Assert.NotNull(state);
        Assert.Equal(outcome.Result.SessionToken, state!.SessionToken);
    }

    private static int GetAvailablePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class StubWorkspaceInfo : IWorkspaceInfo
    {
        public StubWorkspaceInfo(string? workspacePath)
        {
            WorkspacePath = workspacePath;
        }

        public string? WorkspacePath { get; }

        public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;
    }

    private sealed class StubCommands : ICommands
    {
        public IDisposable Register(string commandId, Func<CommandContext, Task> handler)
        {
            return new DummyDisposable();
        }

        public Task ExecuteAsync(string commandId, IReadOnlyList<object?>? args, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    private sealed class StubWindow : IWindow
    {
        private readonly string? _quickPickSelection;

        public StubWindow(string? quickPickSelection = null)
        {
            _quickPickSelection = quickPickSelection;
        }

        public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<QuickPickItem?> ShowQuickPickAsync(IReadOnlyList<QuickPickItem> items, QuickPickOptions options, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_quickPickSelection))
            {
                return Task.FromResult<QuickPickItem?>(null);
            }

            QuickPickItem? item = items.FirstOrDefault(candidate =>
                string.Equals(candidate.Label, _quickPickSelection, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(item);
        }
        public IOutputChannel CreateOutputChannel(string name) => new InMemoryOutputChannel(name);
        public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority) => new InMemoryStatusBarItem();
    }

    private sealed class StubEditorServices : IEditorServices
    {
        private readonly List<IEditorDocument> _documents = new();

        public IEditorDocument? ActiveDocument => _documents.FirstOrDefault();

        public IReadOnlyList<IEditorDocument> GetOpenDocuments() => _documents;

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
        {
            StubEditorDocument doc = new(filePath);
            _documents.Add(doc);
            return Task.FromResult<IEditorDocument?>(doc);
        }

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)
        {
            StubEditorDocument doc = new(filePath);
            _documents.Add(doc);
            return Task.FromResult<IEditorDocument?>(doc);
        }

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;
    }

    private sealed class StubEditorDocument : IEditorDocument
    {
        private string _text = string.Empty;

        public StubEditorDocument(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        public string? LanguageId => "text";

        public int CaretOffset { get; set; }

        public int SelectionStart { get; set; }

        public int SelectionLength { get; set; }

        public Task<string> GetTextAsync(CancellationToken ct)
        {
            return Task.FromResult(_text);
        }

        public Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)
        {
            _text = _text + string.Join("", edits.Select(e => e.NewText));
            return Task.CompletedTask;
        }

        public event EventHandler<EditorDocumentChangedEventArgs>? Changed;

        public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;
    }

    private sealed class StubDiagnostics : IDiagnosticsService
    {
        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());
        }

        public event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;
    }

    private sealed class StubTerminalBridge : ITerminalBridge
    {
        public Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct)
        {
            return Task.FromResult(new TerminalInfo(Guid.NewGuid(), request.Title ?? "terminal"));
        }

        public Task SendTextAsync(Guid terminalId, string text, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DummyDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

#pragma warning restore CS0067
