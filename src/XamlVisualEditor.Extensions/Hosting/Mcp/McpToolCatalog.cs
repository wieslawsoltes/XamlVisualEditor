using System;
using System.Text;
using System.Text.Json;
using System.Linq;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Defines MCP tools backed by IDE services.</summary>
public sealed class McpToolCatalog
{
    private readonly Dictionary<string, McpTool> _tools = new(StringComparer.Ordinal);
    private readonly ICommands _commands;
    private readonly IWorkspace _workspace;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IWindow _window;
    private readonly IEditorServices _editor;
    private readonly IDiagnosticsService _diagnostics;
    private readonly ITerminalBridge _terminal;
    private readonly ISettings _settings;

    public McpToolCatalog(
        ICommands commands,
        IWorkspace workspace,
        IWorkspaceInfo workspaceInfo,
        IWindow window,
        IEditorServices editor,
        IDiagnosticsService diagnostics,
        ITerminalBridge terminal,
        ISettings settings)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _workspaceInfo = workspaceInfo ?? throw new ArgumentNullException(nameof(workspaceInfo));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        RegisterTools();
    }

    public IReadOnlyList<McpToolDefinition> ListTools()
    {
        return _tools.Values.Select(tool => tool.Definition).ToArray();
    }

    public bool TryGet(string name, out McpTool tool)
    {
        return _tools.TryGetValue(name, out tool!);
    }

    private void RegisterTools()
    {
        Register(
            "xve.workspace.findFiles",
            "Find workspace files using glob patterns.",
            new
            {
                type = "object",
                properties = new
                {
                    include = new { type = "string" },
                    exclude = new { type = "string" },
                    maxResults = new { type = "integer" }
                },
                required = new[] { "include" }
            },
            requiresWrite: false,
            HandleFindFilesAsync);

        Register(
            "xve.workspace.readFile",
            "Read a workspace file as UTF-8 text.",
            new
            {
                type = "object",
                properties = new
                {
                    path = new { type = "string" }
                },
                required = new[] { "path" }
            },
            requiresWrite: false,
            HandleReadFileAsync);

        Register(
            "xve.workspace.writeFile",
            "Write a workspace file with UTF-8 text.",
            new
            {
                type = "object",
                properties = new
                {
                    path = new { type = "string" },
                    content = new { type = "string" }
                },
                required = new[] { "path", "content" }
            },
            requiresWrite: true,
            HandleWriteFileAsync);

        Register(
            "xve.workspace.info",
            "Get active workspace metadata.",
            new
            {
                type = "object",
                properties = new { }
            },
            requiresWrite: false,
            HandleWorkspaceInfoAsync);

        Register(
            "xve.commands.list",
            "List registered commands.",
            new
            {
                type = "object",
                properties = new { }
            },
            requiresWrite: false,
            HandleCommandsListAsync);

        Register(
            "xve.commands.execute",
            "Execute a command by id.",
            new
            {
                type = "object",
                properties = new
                {
                    commandId = new { type = "string" },
                    args = new { type = "array" }
                },
                required = new[] { "commandId" }
            },
            requiresWrite: true,
            HandleCommandsExecuteAsync);

        Register(
            "xve.editor.openDocument",
            "Open a document in the editor.",
            new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string" }
                },
                required = new[] { "filePath" }
            },
            requiresWrite: false,
            HandleEditorOpenAsync);

        Register(
            "xve.editor.getText",
            "Get document text.",
            new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string" },
                    useSelection = new { type = "boolean" }
                }
            },
            requiresWrite: false,
            HandleEditorGetTextAsync);

        Register(
            "xve.editor.applyEdits",
            "Apply text edits to a document.",
            new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string" },
                    edits = new { type = "array" }
                },
                required = new[] { "filePath", "edits" }
            },
            requiresWrite: true,
            HandleEditorApplyEditsAsync);

        Register(
            "xve.editor.getSelection",
            "Get the current selection.",
            new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string" }
                }
            },
            requiresWrite: false,
            HandleEditorGetSelectionAsync);

        Register(
            "xve.editor.setSelection",
            "Set the current selection.",
            new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string" },
                    selectionStart = new { type = "integer" },
                    selectionLength = new { type = "integer" },
                    caretOffset = new { type = "integer" }
                },
                required = new[] { "filePath", "selectionStart", "selectionLength" }
            },
            requiresWrite: true,
            HandleEditorSetSelectionAsync);

        Register(
            "xve.diagnostics.get",
            "Get diagnostics for a file or the workspace.",
            new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string" }
                }
            },
            requiresWrite: false,
            HandleDiagnosticsGetAsync);

        Register(
            "xve.window.showMessage",
            "Show a message to the user.",
            new
            {
                type = "object",
                properties = new
                {
                    message = new { type = "string" },
                    severity = new { type = "string" },
                    actions = new { type = "array" }
                },
                required = new[] { "message" }
            },
            requiresWrite: false,
            HandleWindowShowMessageAsync);

        Register(
            "xve.window.inputBox",
            "Show an input box.",
            new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string" },
                    prompt = new { type = "string" },
                    value = new { type = "string" }
                }
            },
            requiresWrite: false,
            HandleWindowInputBoxAsync);

        Register(
            "xve.window.quickPick",
            "Show a quick pick list.",
            new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string" },
                    items = new { type = "array" },
                    canPickMany = new { type = "boolean" }
                },
                required = new[] { "items" }
            },
            requiresWrite: false,
            HandleWindowQuickPickAsync);

        Register(
            "xve.terminal.create",
            "Create a terminal session.",
            new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string" },
                    workingDirectory = new { type = "string" },
                    shellPath = new { type = "string" },
                    arguments = new { type = "array" }
                }
            },
            requiresWrite: true,
            HandleTerminalCreateAsync);

        Register(
            "xve.terminal.sendText",
            "Send text to a terminal.",
            new
            {
                type = "object",
                properties = new
                {
                    terminalId = new { type = "string" },
                    text = new { type = "string" }
                },
                required = new[] { "terminalId", "text" }
            },
            requiresWrite: true,
            HandleTerminalSendTextAsync);

        Register(
            "xve.settings.get",
            "Get a settings value.",
            new
            {
                type = "object",
                properties = new
                {
                    section = new { type = "string" },
                    defaultValue = new { }
                },
                required = new[] { "section" }
            },
            requiresWrite: false,
            HandleSettingsGetAsync);

        Register(
            "xve.settings.update",
            "Update a settings value.",
            new
            {
                type = "object",
                properties = new
                {
                    section = new { type = "string" },
                    value = new { },
                    target = new { type = "string" }
                },
                required = new[] { "section" }
            },
            requiresWrite: true,
            HandleSettingsUpdateAsync);
    }

    private void Register(
        string name,
        string description,
        object inputSchema,
        bool requiresWrite,
        Func<JsonElement?, CancellationToken, Task<McpToolCallResult>> handler)
    {
        McpToolDefinition definition = new(name, description, inputSchema);
        _tools[name] = new McpTool(definition, requiresWrite, handler);
    }

    private async Task<McpToolCallResult> HandleFindFilesAsync(JsonElement? args, CancellationToken ct)
    {
        WorkspaceFindFilesArgs request = Deserialize<WorkspaceFindFilesArgs>(args);
        string include = string.IsNullOrWhiteSpace(request.Include) ? "**/*" : request.Include;
        IReadOnlyList<string> files = await _workspace.FindFilesAsync(include, request.Exclude, ct).ConfigureAwait(false);
        if (request.MaxResults is > 0 && files.Count > request.MaxResults.Value)
        {
            files = files.Take(request.MaxResults.Value).ToArray();
        }

        return ResultJson(new { files });
    }

    private async Task<McpToolCallResult> HandleReadFileAsync(JsonElement? args, CancellationToken ct)
    {
        WorkspaceReadFileArgs request = Deserialize<WorkspaceReadFileArgs>(args);
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new McpJsonRpcException(-32602, "Path is required.");
        }
        byte[] content = await _workspace.ReadFileAsync(request.Path, ct).ConfigureAwait(false);
        string text = Encoding.UTF8.GetString(content);
        return ResultText(text, mimeType: "text/plain");
    }

    private async Task<McpToolCallResult> HandleWriteFileAsync(JsonElement? args, CancellationToken ct)
    {
        WorkspaceWriteFileArgs request = Deserialize<WorkspaceWriteFileArgs>(args);
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new McpJsonRpcException(-32602, "Path is required.");
        }
        byte[] content = Encoding.UTF8.GetBytes(request.Content ?? string.Empty);
        await _workspace.WriteFileAsync(request.Path, content, ct).ConfigureAwait(false);
        return ResultJson(new { ok = true });
    }

    private Task<McpToolCallResult> HandleWorkspaceInfoAsync(JsonElement? args, CancellationToken ct)
    {
        return Task.FromResult(ResultJson(new
        {
            workspacePath = _workspaceInfo.WorkspacePath,
            activeDocument = _editor.ActiveDocument?.FilePath,
            openDocuments = _editor.GetOpenDocuments().Select(d => d.FilePath).ToArray()
        }));
    }

    private async Task<McpToolCallResult> HandleCommandsListAsync(JsonElement? args, CancellationToken ct)
    {
        IReadOnlyList<string> commands = await _commands.GetCommandsAsync(ct).ConfigureAwait(false);
        return ResultJson(new { commands });
    }

    private async Task<McpToolCallResult> HandleCommandsExecuteAsync(JsonElement? args, CancellationToken ct)
    {
        CommandsExecuteArgs request = Deserialize<CommandsExecuteArgs>(args);
        if (string.IsNullOrWhiteSpace(request.CommandId))
        {
            throw new McpJsonRpcException(-32602, "Command id is required.");
        }
        await _commands.ExecuteAsync(request.CommandId, request.Args, ct).ConfigureAwait(false);
        return ResultJson(new { ok = true });
    }

    private async Task<McpToolCallResult> HandleEditorOpenAsync(JsonElement? args, CancellationToken ct)
    {
        EditorOpenArgs request = Deserialize<EditorOpenArgs>(args);
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new McpJsonRpcException(-32602, "File path is required.");
        }
        IEditorDocument? doc = await _editor.OpenDocumentAsync(request.FilePath, ct).ConfigureAwait(false);
        return ResultJson(new { filePath = doc?.FilePath ?? request.FilePath });
    }

    private async Task<McpToolCallResult> HandleEditorGetTextAsync(JsonElement? args, CancellationToken ct)
    {
        EditorGetTextArgs request = Deserialize<EditorGetTextArgs>(args);
        IEditorDocument doc = ResolveDocument(request.FilePath);
        string text = await doc.GetTextAsync(ct).ConfigureAwait(false);
        if (request.UseSelection)
        {
            int start = Math.Clamp(doc.SelectionStart, 0, text.Length);
            int length = Math.Clamp(doc.SelectionLength, 0, text.Length - start);
            text = text.Substring(start, length);
        }

        return ResultText(text, mimeType: "text/plain");
    }

    private async Task<McpToolCallResult> HandleEditorApplyEditsAsync(JsonElement? args, CancellationToken ct)
    {
        EditorApplyEditsArgs request = Deserialize<EditorApplyEditsArgs>(args);
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new McpJsonRpcException(-32602, "File path is required.");
        }
        IEditorDocument doc = ResolveDocument(request.FilePath);
        await doc.ApplyEditsAsync(request.Edits, ct).ConfigureAwait(false);
        return ResultJson(new { ok = true });
    }

    private Task<McpToolCallResult> HandleEditorGetSelectionAsync(JsonElement? args, CancellationToken ct)
    {
        EditorSelectionArgs request = Deserialize<EditorSelectionArgs>(args);
        IEditorDocument doc = ResolveDocument(request.FilePath);
        return Task.FromResult(ResultJson(new
        {
            filePath = doc.FilePath,
            selectionStart = doc.SelectionStart,
            selectionLength = doc.SelectionLength,
            caretOffset = doc.CaretOffset
        }));
    }

    private Task<McpToolCallResult> HandleEditorSetSelectionAsync(JsonElement? args, CancellationToken ct)
    {
        EditorSetSelectionArgs request = Deserialize<EditorSetSelectionArgs>(args);
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new McpJsonRpcException(-32602, "File path is required.");
        }
        IEditorDocument doc = ResolveDocument(request.FilePath);
        doc.SelectionStart = request.SelectionStart;
        doc.SelectionLength = request.SelectionLength;
        if (request.CaretOffset is not null)
        {
            doc.CaretOffset = request.CaretOffset.Value;
        }

        return Task.FromResult(ResultJson(new { ok = true }));
    }

    private async Task<McpToolCallResult> HandleDiagnosticsGetAsync(JsonElement? args, CancellationToken ct)
    {
        DiagnosticsGetArgs request = Deserialize<DiagnosticsGetArgs>(args);
        IReadOnlyList<LanguageDiagnostic> diagnostics = await _diagnostics.GetDiagnosticsAsync(request.FilePath, ct).ConfigureAwait(false);
        return ResultJson(new { diagnostics });
    }

    private async Task<McpToolCallResult> HandleWindowShowMessageAsync(JsonElement? args, CancellationToken ct)
    {
        WindowShowMessageArgs request = Deserialize<WindowShowMessageArgs>(args);
        string severity = request.Severity?.ToLowerInvariant() ?? "info";
        switch (severity)
        {
            case "warning":
                await _window.ShowWarningMessageAsync(request.Message, ct).ConfigureAwait(false);
                break;
            case "error":
                await _window.ShowErrorMessageAsync(request.Message, ct).ConfigureAwait(false);
                break;
            default:
                await _window.ShowInformationMessageAsync(request.Message, ct).ConfigureAwait(false);
                break;
        }

        string? selectedAction = request.Actions is { Count: > 0 } ? request.Actions[0] : null;
        return ResultJson(new { selectedAction });
    }

    private async Task<McpToolCallResult> HandleWindowInputBoxAsync(JsonElement? args, CancellationToken ct)
    {
        WindowInputArgs request = Deserialize<WindowInputArgs>(args);
        string? value = await _window.ShowInputBoxAsync(new InputBoxOptions(request.Title, request.Prompt, request.Value), ct).ConfigureAwait(false);
        return ResultJson(new { value });
    }

    private async Task<McpToolCallResult> HandleWindowQuickPickAsync(JsonElement? args, CancellationToken ct)
    {
        WindowQuickPickArgs request = Deserialize<WindowQuickPickArgs>(args);
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new McpJsonRpcException(-32602, "Items are required.");
        }
        if (request.CanPickMany)
        {
            throw new McpJsonRpcException(-32602, "Multi-select quick pick is not supported.");
        }

        IReadOnlyList<QuickPickItem> items = request.Items
            .Select(item => new QuickPickItem(item.Label, item.Description, null))
            .ToArray();

        QuickPickItem? result = await _window.ShowQuickPickAsync(items, new QuickPickOptions(request.Title, false), ct).ConfigureAwait(false);
        return ResultJson(new { selected = result?.Label });
    }

    private async Task<McpToolCallResult> HandleTerminalCreateAsync(JsonElement? args, CancellationToken ct)
    {
        TerminalCreateArgs request = Deserialize<TerminalCreateArgs>(args);
        TerminalInfo info = await _terminal.CreateAsync(
            new TerminalCreateRequest(request.Title, request.WorkingDirectory, request.ShellPath, request.Arguments),
            ct).ConfigureAwait(false);

        return ResultJson(new { terminalId = info.Id, title = info.Title });
    }

    private async Task<McpToolCallResult> HandleTerminalSendTextAsync(JsonElement? args, CancellationToken ct)
    {
        TerminalSendTextArgs request = Deserialize<TerminalSendTextArgs>(args);
        if (string.IsNullOrWhiteSpace(request.TerminalId))
        {
            throw new McpJsonRpcException(-32602, "Terminal id is required.");
        }
        if (!Guid.TryParse(request.TerminalId, out Guid id))
        {
            throw new McpJsonRpcException(-32602, "Invalid terminal id.");
        }

        await _terminal.SendTextAsync(id, request.Text, ct).ConfigureAwait(false);
        return ResultJson(new { ok = true });
    }

    private Task<McpToolCallResult> HandleSettingsGetAsync(JsonElement? args, CancellationToken ct)
    {
        SettingsGetArgs request = Deserialize<SettingsGetArgs>(args);
        if (string.IsNullOrWhiteSpace(request.Section))
        {
            throw new McpJsonRpcException(-32602, "Section is required.");
        }
        object? value = _settings.Get<object?>(request.Section, request.DefaultValue);
        return Task.FromResult(ResultJson(new { value }));
    }

    private async Task<McpToolCallResult> HandleSettingsUpdateAsync(JsonElement? args, CancellationToken ct)
    {
        SettingsUpdateArgs request = Deserialize<SettingsUpdateArgs>(args);
        if (string.IsNullOrWhiteSpace(request.Section))
        {
            throw new McpJsonRpcException(-32602, "Section is required.");
        }
        SettingsTarget target = string.Equals(request.Target, "workspace", StringComparison.OrdinalIgnoreCase)
            ? SettingsTarget.Workspace
            : SettingsTarget.User;

        await _settings.UpdateAsync(request.Section, request.Value, target, ct).ConfigureAwait(false);
        return ResultJson(new { ok = true });
    }

    private IEditorDocument ResolveDocument(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            IEditorDocument? active = _editor.ActiveDocument;
            if (active is null)
            {
                throw new McpJsonRpcException(-32000, "No active document.");
            }

            return active;
        }

        IEditorDocument? doc = _editor.GetOpenDocuments().FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (doc is not null)
        {
            return doc;
        }

        throw new McpJsonRpcException(-32000, "Document is not open.");
    }

    private static T Deserialize<T>(JsonElement? args)
    {
        if (args is null)
        {
            throw new McpJsonRpcException(-32602, "Invalid params.");
        }

        return JsonSerializer.Deserialize<T>(args.Value.GetRawText(), McpMessageFraming.SerializerOptions)
            ?? throw new McpJsonRpcException(-32602, "Invalid params.");
    }

    private static McpToolCallResult ResultJson(object payload)
    {
        return new McpToolCallResult(new[]
        {
            new McpContent("text", Text: JsonSerializer.Serialize(payload, McpMessageFraming.SerializerOptions))
        });
    }

    private static McpToolCallResult ResultText(string text, string? mimeType = null)
    {
        return new McpToolCallResult(new[]
        {
            new McpContent("text", Text: text, MimeType: mimeType)
        });
    }

    public sealed record McpTool(McpToolDefinition Definition, bool RequiresWrite, Func<JsonElement?, CancellationToken, Task<McpToolCallResult>> Handler);

    private sealed record WorkspaceFindFilesArgs(string? Include, string? Exclude, int? MaxResults);
    private sealed record WorkspaceReadFileArgs(string Path);
    private sealed record WorkspaceWriteFileArgs(string Path, string? Content);
    private sealed record CommandsExecuteArgs(string CommandId, IReadOnlyList<object?>? Args);
    private sealed record EditorOpenArgs(string FilePath);
    private sealed record EditorGetTextArgs(string? FilePath, bool UseSelection);
    private sealed record EditorApplyEditsArgs(string FilePath, IReadOnlyList<TextEdit> Edits);
    private sealed record EditorSelectionArgs(string? FilePath);
    private sealed record EditorSetSelectionArgs(string FilePath, int SelectionStart, int SelectionLength, int? CaretOffset);
    private sealed record DiagnosticsGetArgs(string? FilePath);
    private sealed record WindowShowMessageArgs(string Message, string? Severity, IReadOnlyList<string>? Actions);
    private sealed record WindowInputArgs(string? Title, string? Prompt, string? Value);
    private sealed record WindowQuickPickArgs(string? Title, IReadOnlyList<WindowQuickPickItem> Items, bool CanPickMany);
    private sealed record WindowQuickPickItem(string Label, string? Description);
    private sealed record TerminalCreateArgs(string? Title, string? WorkingDirectory, string? ShellPath, IReadOnlyList<string>? Arguments);
    private sealed record TerminalSendTextArgs(string TerminalId, string Text);
    private sealed record SettingsGetArgs(string Section, object? DefaultValue);
    private sealed record SettingsUpdateArgs(string Section, object? Value, string? Target);
}
