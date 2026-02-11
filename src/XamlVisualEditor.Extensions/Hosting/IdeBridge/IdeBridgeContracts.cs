using System;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Protocol constants for the IDE bridge.</summary>
public static class IdeBridgeProtocol
{
    /// <summary>Current protocol version.</summary>
    public const string ProtocolVersion = "1.0";

    /// <summary>Initialize the bridge session.</summary>
    public const string InitializeMethod = "bridge.initialize";

    /// <summary>Shutdown the bridge session.</summary>
    public const string ShutdownMethod = "bridge.shutdown";

    /// <summary>List open workspaces.</summary>
    public const string WorkspaceListMethod = "workspace.list";

    /// <summary>Get active workspace.</summary>
    public const string WorkspaceGetActiveMethod = "workspace.getActive";

    /// <summary>Find workspace files.</summary>
    public const string WorkspaceFindFilesMethod = "workspace.findFiles";

    /// <summary>Open a document.</summary>
    public const string DocumentOpenMethod = "document.open";

    /// <summary>Get document text.</summary>
    public const string DocumentGetTextMethod = "document.getText";

    /// <summary>Apply document edits.</summary>
    public const string DocumentApplyEditsMethod = "document.applyEdits";

    /// <summary>Save a document.</summary>
    public const string DocumentSaveMethod = "document.save";

    /// <summary>Get current selection.</summary>
    public const string SelectionGetMethod = "selection.get";

    /// <summary>Set current selection.</summary>
    public const string SelectionSetMethod = "selection.set";

    /// <summary>List registered commands.</summary>
    public const string CommandsListMethod = "commands.list";

    /// <summary>Execute a command.</summary>
    public const string CommandsExecuteMethod = "commands.execute";

    /// <summary>Get diagnostics.</summary>
    public const string DiagnosticsGetMethod = "diagnostics.get";

    /// <summary>Show a message.</summary>
    public const string UiShowMessageMethod = "ui.showMessage";

    /// <summary>Show a quick pick.</summary>
    public const string UiPickMethod = "ui.pick";

    /// <summary>Show an input box.</summary>
    public const string UiInputMethod = "ui.input";

    /// <summary>Create a terminal.</summary>
    public const string TerminalCreateMethod = "terminal.create";

    /// <summary>Send text to a terminal.</summary>
    public const string TerminalSendTextMethod = "terminal.sendText";

    /// <summary>Workspace changed notification.</summary>
    public const string WorkspaceChangedNotification = "workspace.changed";

    /// <summary>Document changed notification.</summary>
    public const string DocumentChangedNotification = "document.changed";

    /// <summary>Diagnostics changed notification.</summary>
    public const string DiagnosticsChangedNotification = "diagnostics.changed";

    /// <summary>Selection changed notification.</summary>
    public const string SelectionChangedNotification = "selection.changed";
}

/// <summary>Initialize request parameters.</summary>
public sealed record BridgeInitializeParams(
    string? SessionToken,
    string? WorkspaceId,
    string? ClientName,
    string? ClientVersion);

/// <summary>Initialize response payload.</summary>
public sealed record BridgeInitializeResult(
    string ProtocolVersion,
    IdeBridgeCapabilities Capabilities,
    string WorkspaceId,
    string SessionToken);

/// <summary>Represents the feature set granted to a client.</summary>
public sealed record IdeBridgeCapabilities(
    bool Files,
    bool Commands,
    bool Diagnostics,
    bool Terminal,
    bool Ui,
    bool Documents,
    bool Selection,
    bool Workspace,
    bool Write);

/// <summary>Workspace descriptor.</summary>
public sealed record WorkspaceDescriptor(
    string Id,
    string Path,
    string? Name);

/// <summary>Workspace list response.</summary>
public sealed record WorkspaceListResult(
    IReadOnlyList<WorkspaceDescriptor> Workspaces);

/// <summary>Active workspace response.</summary>
public sealed record WorkspaceActiveResult(
    WorkspaceDescriptor Workspace);

/// <summary>Find files request parameters.</summary>
public sealed record WorkspaceFindFilesParams(
    string Pattern,
    string? Root,
    bool IncludeHidden,
    int? MaxResults);

/// <summary>Find files response.</summary>
public sealed record WorkspaceFindFilesResult(
    IReadOnlyList<string> Files);

/// <summary>Document open request parameters.</summary>
public sealed record DocumentOpenParams(
    string FilePath);

/// <summary>Document text request parameters.</summary>
public sealed record DocumentGetTextParams(
    string? FilePath,
    bool UseSelection);

/// <summary>Document text response.</summary>
public sealed record DocumentGetTextResult(
    string Text);

/// <summary>Document edits request parameters.</summary>
public sealed record DocumentApplyEditsParams(
    string FilePath,
    IReadOnlyList<TextEdit> Edits);

/// <summary>Document save request parameters.</summary>
public sealed record DocumentSaveParams(
    string FilePath);

/// <summary>Selection request parameters.</summary>
public sealed record SelectionGetParams(
    string? FilePath);

/// <summary>Selection response payload.</summary>
public sealed record SelectionResult(
    string FilePath,
    int SelectionStart,
    int SelectionLength,
    int CaretOffset);

/// <summary>Selection set request parameters.</summary>
public sealed record SelectionSetParams(
    string FilePath,
    int SelectionStart,
    int SelectionLength,
    int? CaretOffset);

/// <summary>Commands list response.</summary>
public sealed record CommandsListResult(
    IReadOnlyList<string> Commands);

/// <summary>Command execute request parameters.</summary>
public sealed record CommandsExecuteParams(
    string CommandId,
    IReadOnlyList<object?>? Arguments);

/// <summary>Diagnostics get request parameters.</summary>
public sealed record DiagnosticsGetParams(
    string? FilePath);

/// <summary>Diagnostics response.</summary>
public sealed record DiagnosticsGetResult(
    IReadOnlyList<LanguageDiagnostic> Diagnostics);

/// <summary>Message request parameters.</summary>
public sealed record UiShowMessageParams(
    string Text,
    string? Severity,
    IReadOnlyList<string>? Actions);

/// <summary>Message response payload.</summary>
public sealed record UiShowMessageResult(
    string? SelectedAction);

/// <summary>Quick pick request parameters.</summary>
public sealed record UiPickParams(
    string? Title,
    IReadOnlyList<UiPickItem> Items,
    bool CanPickMany);

/// <summary>Quick pick item.</summary>
public sealed record UiPickItem(
    string Id,
    string Label,
    string? Description);

/// <summary>Quick pick response payload.</summary>
public sealed record UiPickResult(
    IReadOnlyList<string> SelectedIds);

/// <summary>Input box request parameters.</summary>
public sealed record UiInputParams(
    string? Title,
    string? Prompt,
    string? Placeholder,
    string? Value,
    bool IsPassword);

/// <summary>Input box response payload.</summary>
public sealed record UiInputResult(
    string? Value);

/// <summary>Terminal create response.</summary>
public sealed record TerminalCreateResult(
    Guid TerminalId,
    string Title);

/// <summary>Terminal send text parameters.</summary>
public sealed record TerminalSendTextParams(
    Guid TerminalId,
    string Text);

/// <summary>Workspace change notification payload.</summary>
public sealed record WorkspaceChangedParams(
    string? WorkspaceId);

/// <summary>Document change notification payload.</summary>
public sealed record DocumentChangedParams(
    string FilePath);

/// <summary>Diagnostics change notification payload.</summary>
public sealed record DiagnosticsChangedParams(
    string? FilePath);

/// <summary>Selection change notification payload.</summary>
public sealed record SelectionChangedParams(
    string FilePath,
    int SelectionStart,
    int SelectionLength,
    int CaretOffset);
