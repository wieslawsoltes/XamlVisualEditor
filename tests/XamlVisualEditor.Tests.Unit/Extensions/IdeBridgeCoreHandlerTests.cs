using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;
using Xunit;

#pragma warning disable CS0067 // Events required by interfaces for test doubles.

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class IdeBridgeCoreHandlerTests
{
    [Fact]
    public async Task CommandsListReturnsCommands()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using IdeBridgeJsonRpcConnection connection = pipe.CreateConnection();

        IdeBridgeSessionRegistry registry = new();
        registry.Set(connection, new IdeBridgeSessionInfo("ws", FullCapabilities(), "token"));

        FakeCommands commands = new(new[] { "xve.test" });
        FakeWorkspace workspace = new();
        FakeWorkspaceInfo workspaceInfo = new();
        FakeWindow window = new();
        FakeEditorServices editor = new();
        FakeDiagnostics diagnostics = new();
        FakeTerminal terminal = new();

        IdeBridgeCoreHandler handler = new(
            commands,
            workspace,
            workspaceInfo,
            window,
            editor,
            diagnostics,
            terminal,
            registry);
        handler.Register(connection);
        connection.Start(cts.Token);

        await IdeBridgeMessageFraming.WriteMessageAsync(pipe.ServerWriter, new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "commands.list",
            @params = new { }
        }, cts.Token);

        using JsonDocument response = await IdeBridgeMessageFraming.ReadMessageAsync(pipe.ServerReader, cts.Token);
        JsonElement root = response.RootElement;
        Assert.Equal(1, root.GetProperty("id").GetInt32());
        JsonElement result = root.GetProperty("result");
        Assert.Equal("xve.test", result.GetProperty("commands")[0].GetString());
    }

    [Fact]
    public async Task ApplyEditsRequiresWritePermission()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using IdeBridgeJsonRpcConnection connection = pipe.CreateConnection();

        IdeBridgeSessionRegistry registry = new();
        registry.Set(connection, new IdeBridgeSessionInfo("ws", ReadOnlyCapabilities(), "token"));

        FakeCommands commands = new(Array.Empty<string>());
        FakeWorkspace workspace = new();
        FakeWorkspaceInfo workspaceInfo = new();
        FakeWindow window = new();
        FakeEditorServices editor = new(new FakeEditorDocument("/tmp/a.txt"));
        FakeDiagnostics diagnostics = new();
        FakeTerminal terminal = new();

        IdeBridgeCoreHandler handler = new(
            commands,
            workspace,
            workspaceInfo,
            window,
            editor,
            diagnostics,
            terminal,
            registry);
        handler.Register(connection);
        connection.Start(cts.Token);

        await IdeBridgeMessageFraming.WriteMessageAsync(pipe.ServerWriter, new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "document.applyEdits",
            @params = new
            {
                filePath = "/tmp/a.txt",
                edits = new[]
                {
                    new { offset = 0, length = 0, newText = "hi" }
                }
            }
        }, cts.Token);

        using JsonDocument response = await IdeBridgeMessageFraming.ReadMessageAsync(pipe.ServerReader, cts.Token);
        JsonElement error = response.RootElement.GetProperty("error");
        Assert.Equal(-32000, error.GetProperty("code").GetInt32());
    }

    private static IdeBridgeCapabilities FullCapabilities()
    {
        return new IdeBridgeCapabilities(true, true, true, true, true, true, true, true, true);
    }

    private static IdeBridgeCapabilities ReadOnlyCapabilities()
    {
        return new IdeBridgeCapabilities(true, false, true, false, true, true, true, true, false);
    }

    private sealed class DuplexPipe : IDisposable
    {
        private readonly Pipe _clientToServer = new();
        private readonly Pipe _serverToClient = new();

        public Stream ServerReader => _clientToServer.Reader.AsStream();
        public Stream ServerWriter => _serverToClient.Writer.AsStream();

        public IdeBridgeJsonRpcConnection CreateConnection()
        {
            return new IdeBridgeJsonRpcConnection(_serverToClient.Reader.AsStream(), _clientToServer.Writer.AsStream());
        }

        public void Dispose()
        {
            _clientToServer.Reader.Complete();
            _clientToServer.Writer.Complete();
            _serverToClient.Reader.Complete();
            _serverToClient.Writer.Complete();
        }
    }

    private sealed class FakeCommands : ICommands
    {
        private readonly IReadOnlyList<string> _commands;

        public FakeCommands(IReadOnlyList<string> commands)
        {
            _commands = commands;
        }

        public IDisposable Register(string commandId, Func<CommandContext, Task> handler) => new DummyDisposable();

        public Task ExecuteAsync(string commandId, IReadOnlyList<object?>? args, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken) => Task.FromResult(_commands);
    }

    private sealed class FakeWorkspace : IWorkspace
    {
        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        public Task<IReadOnlyList<string>> FindFilesAsync(string includeGlob, string? excludeGlob, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(Array.Empty<byte>());

        public Task WriteFileAsync(string path, byte[] content, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public IFileSystemWatcher CreateFileSystemWatcher(string glob) => new DummyWatcher();
    }

    private sealed class FakeWorkspaceInfo : IWorkspaceInfo
    {
        public string? WorkspacePath => "/tmp";

        public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;
    }

    private sealed class FakeWindow : IWindow
    {
        public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<QuickPickItem?> ShowQuickPickAsync(IReadOnlyList<QuickPickItem> items, QuickPickOptions options, CancellationToken cancellationToken)
            => Task.FromResult<QuickPickItem?>(null);

        public IOutputChannel CreateOutputChannel(string name) => new DummyOutputChannel(name);

        public Task<IReadOnlyList<OutputChannelInfo>> GetOutputChannelsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<OutputChannelInfo>>(Array.Empty<OutputChannelInfo>());

        public event EventHandler<OutputChannelEventArgs>? OutputChannelCreated;

        public event EventHandler<OutputChannelEventArgs>? OutputChannelRemoved;

        public event EventHandler<OutputChannelMessageEventArgs>? OutputChannelMessage;

        public event EventHandler<OutputChannelClearedEventArgs>? OutputChannelCleared;

        public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority) => new DummyStatusBarItem();
    }

    private sealed class FakeEditorServices : IEditorServices
    {
        private readonly IEditorDocument? _document;

        public FakeEditorServices(IEditorDocument? document = null)
        {
            _document = document;
        }

        public IEditorDocument? ActiveDocument => _document;

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;

        public IReadOnlyList<IEditorDocument> GetOpenDocuments()
            => _document is null ? Array.Empty<IEditorDocument>() : new[] { _document };

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
            => Task.FromResult(_document);

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)
            => Task.FromResult(_document);

        public Task<bool> OpenLocationAsync(LanguageLocation location, CancellationToken ct)
            => Task.FromResult(false);
    }

    private sealed class FakeEditorDocument : IEditorDocument
    {
        public FakeEditorDocument(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        public string? LanguageId => null;

        public int CaretOffset { get; set; }

        public int SelectionStart { get; set; }

        public int SelectionLength { get; set; }

        public event EventHandler<EditorDocumentChangedEventArgs>? Changed;

        public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;

        public Task<string> GetTextAsync(CancellationToken ct) => Task.FromResult(string.Empty);

        public Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeDiagnostics : IDiagnosticsService
    {
        public event EventHandler<DiagnosticsChannelsChangedEventArgs>? ChannelsChanged;

        public event EventHandler<DiagnosticsChannelPublishedEventArgs>? DiagnosticsChannelPublished;

        public event EventHandler<DiagnosticsSnapshotPublishedEventArgs>? DiagnosticsSnapshotPublished;

        public event EventHandler<DiagnosticsPublishedEventArgs>? DiagnosticsPublished;

        public event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(DiagnosticsQuery query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());

        public Task<IReadOnlyList<DiagnosticsDocumentSnapshot>> GetDiagnosticsSnapshotAsync(
            DiagnosticsQuery query,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DiagnosticsDocumentSnapshot>>(Array.Empty<DiagnosticsDocumentSnapshot>());

        public Task<IReadOnlyList<DiagnosticsChannelInfo>> GetChannelsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DiagnosticsChannelInfo>>(Array.Empty<DiagnosticsChannelInfo>());
    }

    private sealed class FakeTerminal : ITerminalBridge
    {
        public Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct)
            => Task.FromResult(new TerminalInfo(Guid.NewGuid(), "terminal"));

        public Task SendTextAsync(Guid terminalId, string text, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class DummyDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class DummyWatcher : IFileSystemWatcher
    {
        public event EventHandler<string>? Created;
        public event EventHandler<string>? Changed;
        public event EventHandler<string>? Deleted;

        public void Dispose()
        {
        }
    }

    private sealed class DummyOutputChannel : IOutputChannel
    {
        public DummyOutputChannel(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public void Append(string value)
        {
        }

        public void AppendLine(string value)
        {
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Clear()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class DummyStatusBarItem : IStatusBarItem
    {
        public string Text { get; set; } = string.Empty;
        public string? Tooltip { get; set; }
        public string? CommandId { get; set; }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Dispose()
        {
        }
    }
}

#pragma warning restore CS0067
