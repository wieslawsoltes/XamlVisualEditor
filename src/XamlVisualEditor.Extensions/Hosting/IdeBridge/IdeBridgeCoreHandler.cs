using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Handles core IDE bridge requests.</summary>
public sealed class IdeBridgeCoreHandler : IIdeBridgeRequestHandler
{
    private readonly ICommands _commands;
    private readonly IWorkspace _workspace;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IWindow _window;
    private readonly IEditorServices _editor;
    private readonly IDiagnosticsService _diagnostics;
    private readonly ITerminalBridge _terminal;
    private readonly IdeBridgeSessionRegistry _sessions;

    /// <summary>Creates the core handler.</summary>
    public IdeBridgeCoreHandler(
        ICommands commands,
        IWorkspace workspace,
        IWorkspaceInfo workspaceInfo,
        IWindow window,
        IEditorServices editor,
        IDiagnosticsService diagnostics,
        ITerminalBridge terminal,
        IdeBridgeSessionRegistry sessions)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _workspaceInfo = workspaceInfo ?? throw new ArgumentNullException(nameof(workspaceInfo));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    /// <inheritdoc />
    public void Register(IdeBridgeJsonRpcConnection connection)
    {
        connection.RegisterRequestHandler(IdeBridgeProtocol.WorkspaceListMethod, (_, ct) => HandleWorkspaceListAsync(connection, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.WorkspaceGetActiveMethod, (_, ct) => HandleWorkspaceGetActiveAsync(connection, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.WorkspaceFindFilesMethod, (p, ct) => HandleWorkspaceFindFilesAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.DocumentOpenMethod, (p, ct) => HandleDocumentOpenAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.DocumentGetTextMethod, (p, ct) => HandleDocumentGetTextAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.DocumentApplyEditsMethod, (p, ct) => HandleDocumentApplyEditsAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.DocumentSaveMethod, (p, ct) => HandleDocumentSaveAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.SelectionGetMethod, (p, ct) => HandleSelectionGetAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.SelectionSetMethod, (p, ct) => HandleSelectionSetAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.CommandsListMethod, (_, ct) => HandleCommandsListAsync(connection, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.CommandsExecuteMethod, (p, ct) => HandleCommandsExecuteAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.DiagnosticsGetMethod, (p, ct) => HandleDiagnosticsGetAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.UiShowMessageMethod, (p, ct) => HandleUiShowMessageAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.UiPickMethod, (p, ct) => HandleUiPickAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.UiInputMethod, (p, ct) => HandleUiInputAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.TerminalCreateMethod, (p, ct) => HandleTerminalCreateAsync(connection, p, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.TerminalSendTextMethod, (p, ct) => HandleTerminalSendTextAsync(connection, p, ct));

        HookNotifications(connection);
    }

    private void HookNotifications(IdeBridgeJsonRpcConnection connection)
    {
        IEditorDocument? currentDocument = _editor.ActiveDocument;

        void OnActiveDocumentChanged(object? sender, EditorActiveDocumentChangedEventArgs args)
        {
            currentDocument = args.Document;
            if (args.Document is not null)
            {
                _ = connection.SendNotificationAsync(
                    IdeBridgeProtocol.DocumentChangedNotification,
                    new DocumentChangedParams(args.Document.FilePath),
                    CancellationToken.None);
            }
        }

        void OnDocumentChanged(object? sender, EditorDocumentChangedEventArgs args)
        {
            if (!TryGetSession(connection, out _))
            {
                return;
            }

            _ = connection.SendNotificationAsync(
                IdeBridgeProtocol.DocumentChangedNotification,
                new DocumentChangedParams(args.FilePath),
                CancellationToken.None);
        }

        void OnSelectionChanged(object? sender, EditorSelectionChangedEventArgs args)
        {
            if (!TryGetSession(connection, out _))
            {
                return;
            }

            _ = connection.SendNotificationAsync(
                IdeBridgeProtocol.SelectionChangedNotification,
                new SelectionChangedParams(args.FilePath, args.SelectionStart, args.SelectionLength, args.SelectionStart + args.SelectionLength),
                CancellationToken.None);
        }

        void OnDiagnosticsChanged(object? sender, DiagnosticsChangedEventArgs args)
        {
            if (!TryGetSession(connection, out _))
            {
                return;
            }

            _ = connection.SendNotificationAsync(
                IdeBridgeProtocol.DiagnosticsChangedNotification,
                new DiagnosticsChangedParams(args.FilePath),
                CancellationToken.None);
        }

        void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
        {
            if (!TryGetSession(connection, out _))
            {
                return;
            }

            _ = connection.SendNotificationAsync(
                IdeBridgeProtocol.WorkspaceChangedNotification,
                new WorkspaceChangedParams(ResolveWorkspaceId(args.WorkspacePath)),
                CancellationToken.None);
        }

        if (currentDocument is not null)
        {
            currentDocument.Changed += OnDocumentChanged;
            currentDocument.SelectionChanged += OnSelectionChanged;
        }

        _editor.ActiveDocumentChanged += OnActiveDocumentChanged;
        _diagnostics.DiagnosticsChanged += OnDiagnosticsChanged;
        _workspaceInfo.WorkspaceChanged += OnWorkspaceChanged;

        connection.Disconnected += _ =>
        {
            if (currentDocument is not null)
            {
                currentDocument.Changed -= OnDocumentChanged;
                currentDocument.SelectionChanged -= OnSelectionChanged;
            }

            _editor.ActiveDocumentChanged -= OnActiveDocumentChanged;
            _diagnostics.DiagnosticsChanged -= OnDiagnosticsChanged;
            _workspaceInfo.WorkspaceChanged -= OnWorkspaceChanged;
        };
    }

    private Task<object?> HandleWorkspaceListAsync(IdeBridgeJsonRpcConnection connection, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Workspace, "workspace");

        string? path = _workspaceInfo.WorkspacePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult<object?>(new WorkspaceListResult(Array.Empty<WorkspaceDescriptor>()));
        }

        WorkspaceDescriptor workspace = new(ResolveWorkspaceId(path), path, Path.GetFileName(path));
        return Task.FromResult<object?>(new WorkspaceListResult(new[] { workspace }));
    }

    private Task<object?> HandleWorkspaceGetActiveAsync(IdeBridgeJsonRpcConnection connection, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Workspace, "workspace");

        string? path = _workspaceInfo.WorkspacePath;
        WorkspaceDescriptor workspace = new(
            ResolveWorkspaceId(path),
            path ?? string.Empty,
            path is null ? null : Path.GetFileName(path));

        return Task.FromResult<object?>(new WorkspaceActiveResult(workspace));
    }

    private async Task<object?> HandleWorkspaceFindFilesAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Files, "files");

        WorkspaceFindFilesParams request = Deserialize<WorkspaceFindFilesParams>(parameters);
        string pattern = string.IsNullOrWhiteSpace(request.Pattern) ? "**/*" : request.Pattern;
        IReadOnlyList<string> files = await _workspace.FindFilesAsync(pattern, null, ct).ConfigureAwait(false);

        if (request.MaxResults is not null && request.MaxResults.Value > 0 && files.Count > request.MaxResults.Value)
        {
            files = files.Take(request.MaxResults.Value).ToArray();
        }

        return new WorkspaceFindFilesResult(files);
    }

    private async Task<object?> HandleDocumentOpenAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Documents, "documents");

        DocumentOpenParams request = Deserialize<DocumentOpenParams>(parameters);
        IEditorDocument? doc = await _editor.OpenDocumentAsync(request.FilePath, ct).ConfigureAwait(false);
        return new { filePath = doc?.FilePath ?? request.FilePath };
    }

    private async Task<object?> HandleDocumentGetTextAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Documents, "documents");

        DocumentGetTextParams request = Deserialize<DocumentGetTextParams>(parameters);
        IEditorDocument doc = await ResolveDocumentAsync(request.FilePath, ct).ConfigureAwait(false);
        string text = await doc.GetTextAsync(ct).ConfigureAwait(false);
        return new DocumentGetTextResult(text);
    }

    private async Task<object?> HandleDocumentApplyEditsAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Documents, "documents");
        RequireWrite(session);

        DocumentApplyEditsParams request = Deserialize<DocumentApplyEditsParams>(parameters);
        IEditorDocument doc = await ResolveDocumentAsync(request.FilePath, ct).ConfigureAwait(false);
        await doc.ApplyEditsAsync(request.Edits, ct).ConfigureAwait(false);
        return new { };
    }

    private async Task<object?> HandleDocumentSaveAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Documents, "documents");
        RequireWrite(session);

        DocumentSaveParams request = Deserialize<DocumentSaveParams>(parameters);
        IEditorDocument doc = await ResolveDocumentAsync(request.FilePath, ct).ConfigureAwait(false);
        string text = await doc.GetTextAsync(ct).ConfigureAwait(false);
        await _workspace.WriteFileAsync(request.FilePath, System.Text.Encoding.UTF8.GetBytes(text), ct).ConfigureAwait(false);
        return new { };
    }

    private Task<object?> HandleSelectionGetAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Selection, "selection");

        SelectionGetParams request = Deserialize<SelectionGetParams>(parameters);
        IEditorDocument doc = ResolveDocument(request.FilePath);
        SelectionResult result = new(doc.FilePath, doc.SelectionStart, doc.SelectionLength, doc.CaretOffset);
        return Task.FromResult<object?>(result);
    }

    private Task<object?> HandleSelectionSetAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Selection, "selection");

        SelectionSetParams request = Deserialize<SelectionSetParams>(parameters);
        IEditorDocument doc = ResolveDocument(request.FilePath);
        doc.SelectionStart = request.SelectionStart;
        doc.SelectionLength = request.SelectionLength;
        if (request.CaretOffset is not null)
        {
            doc.CaretOffset = request.CaretOffset.Value;
        }

        return Task.FromResult<object?>(new { });
    }

    private async Task<object?> HandleCommandsListAsync(IdeBridgeJsonRpcConnection connection, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Commands, "commands");

        IReadOnlyList<string> commands = await _commands.GetCommandsAsync(ct).ConfigureAwait(false);
        return new CommandsListResult(commands);
    }

    private async Task<object?> HandleCommandsExecuteAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Commands, "commands");
        RequireWrite(session);

        CommandsExecuteParams request = Deserialize<CommandsExecuteParams>(parameters);
        await _commands.ExecuteAsync(request.CommandId, request.Arguments, ct).ConfigureAwait(false);
        return new { };
    }

    private async Task<object?> HandleDiagnosticsGetAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Diagnostics, "diagnostics");

        DiagnosticsGetParams request = Deserialize<DiagnosticsGetParams>(parameters);
        IReadOnlyList<LanguageDiagnostic> diagnostics = await _diagnostics.GetDiagnosticsAsync(request.FilePath, ct).ConfigureAwait(false);
        return new DiagnosticsGetResult(diagnostics);
    }

    private async Task<object?> HandleUiShowMessageAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Ui, "ui");

        UiShowMessageParams request = Deserialize<UiShowMessageParams>(parameters);
        if (request.Actions is null || request.Actions.Count == 0)
        {
            await ShowMessageAsync(request, ct).ConfigureAwait(false);
            return new UiShowMessageResult(null);
        }

        IReadOnlyList<QuickPickItem> items = request.Actions
            .Select(action => new QuickPickItem(action, null, null))
            .ToArray();

        QuickPickItem? selected = await _window
            .ShowQuickPickAsync(items, new QuickPickOptions(null, false), ct)
            .ConfigureAwait(false);

        return new UiShowMessageResult(selected?.Label);
    }

    private async Task<object?> HandleUiPickAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Ui, "ui");

        UiPickParams request = Deserialize<UiPickParams>(parameters);
        if (request.CanPickMany)
        {
            throw new IdeBridgeJsonRpcException(-32602, "Multi-select pick is not supported.");
        }

        IReadOnlyList<QuickPickItem> items = request.Items
            .Select(item => new QuickPickItem(item.Label, item.Description, null))
            .ToArray();

        QuickPickItem? selected = await _window
            .ShowQuickPickAsync(items, new QuickPickOptions(request.Title, false), ct)
            .ConfigureAwait(false);

        string? selectedId = null;
        if (selected is not null)
        {
            UiPickItem? match = request.Items.FirstOrDefault(item => string.Equals(item.Label, selected.Label, StringComparison.Ordinal));
            selectedId = match?.Id ?? selected.Label;
        }

        return new UiPickResult(selectedId is null ? Array.Empty<string>() : new[] { selectedId });
    }

    private async Task<object?> HandleUiInputAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Ui, "ui");

        UiInputParams request = Deserialize<UiInputParams>(parameters);
        string? value = await _window
            .ShowInputBoxAsync(new InputBoxOptions(request.Title, request.Prompt, request.Value), ct)
            .ConfigureAwait(false);

        return new UiInputResult(value);
    }

    private async Task<object?> HandleTerminalCreateAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Terminal, "terminal");
        RequireWrite(session);

        TerminalCreateRequest request = Deserialize<TerminalCreateRequest>(parameters);
        TerminalInfo info = await _terminal.CreateAsync(request, ct).ConfigureAwait(false);
        return new TerminalCreateResult(info.Id, info.Title);
    }

    private async Task<object?> HandleTerminalSendTextAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        IdeBridgeSessionInfo session = RequireSession(connection);
        RequireCapability(session, c => c.Terminal, "terminal");
        RequireWrite(session);

        TerminalSendTextParams request = Deserialize<TerminalSendTextParams>(parameters);
        await _terminal.SendTextAsync(request.TerminalId, request.Text, ct).ConfigureAwait(false);
        return new { };
    }

    private async Task ShowMessageAsync(UiShowMessageParams request, CancellationToken ct)
    {
        string severity = request.Severity?.ToLowerInvariant() ?? "info";
        switch (severity)
        {
            case "warning":
                await _window.ShowWarningMessageAsync(request.Text, ct).ConfigureAwait(false);
                break;
            case "error":
                await _window.ShowErrorMessageAsync(request.Text, ct).ConfigureAwait(false);
                break;
            default:
                await _window.ShowInformationMessageAsync(request.Text, ct).ConfigureAwait(false);
                break;
        }
    }

    private IdeBridgeSessionInfo RequireSession(IdeBridgeJsonRpcConnection connection)
    {
        if (!_sessions.TryGet(connection, out IdeBridgeSessionInfo session))
        {
            throw new IdeBridgeJsonRpcException(-32000, "Session not initialized.");
        }

        return session;
    }

    private static void RequireCapability(IdeBridgeSessionInfo session, Func<IdeBridgeCapabilities, bool> selector, string name)
    {
        if (!selector(session.Capabilities))
        {
            throw new IdeBridgeJsonRpcException(-32000, $"Permission denied for {name}.");
        }
    }

    private static void RequireWrite(IdeBridgeSessionInfo session)
    {
        if (!session.Capabilities.Write)
        {
            throw new IdeBridgeJsonRpcException(-32000, "Write permission denied.");
        }
    }

    private bool TryGetSession(IdeBridgeJsonRpcConnection connection, out IdeBridgeSessionInfo session)
    {
        return _sessions.TryGet(connection, out session);
    }

    private static T Deserialize<T>(JsonElement? parameters)
    {
        if (parameters is null)
        {
            throw new IdeBridgeJsonRpcException(-32602, "Missing parameters.");
        }

        T? value = JsonSerializer.Deserialize<T>(parameters.Value.GetRawText(), IdeBridgeMessageFraming.SerializerOptions);
        if (value is null)
        {
            throw new IdeBridgeJsonRpcException(-32602, "Invalid parameters.");
        }

        return value;
    }

    private async Task<IEditorDocument> ResolveDocumentAsync(string? filePath, CancellationToken ct)
    {
        IEditorDocument? doc = ResolveDocument(filePath, allowNull: true);
        if (doc is not null)
        {
            return doc;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new IdeBridgeJsonRpcException(-32002, "Document not found.");
        }

        IEditorDocument? opened = await _editor.OpenDocumentAsync(filePath, ct).ConfigureAwait(false);
        if (opened is null)
        {
            throw new IdeBridgeJsonRpcException(-32002, "Document not found.");
        }

        return opened;
    }

    private IEditorDocument ResolveDocument(string? filePath, bool allowNull = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            if (_editor.ActiveDocument is not null)
            {
                return _editor.ActiveDocument;
            }

            if (allowNull)
            {
                return null!;
            }

            throw new IdeBridgeJsonRpcException(-32002, "Document not found.");
        }

        IEditorDocument? doc = _editor.GetOpenDocuments()
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (doc is null)
        {
            throw new IdeBridgeJsonRpcException(-32002, "Document not found.");
        }

        return doc;
    }

    private string ResolveWorkspaceId(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "default";
        }

        return Path.GetFullPath(path);
    }
}
