# Extension SDK API Reference

This is the current native .NET extension API surface. All types live in the
`XamlVisualEditor.Extensions` namespace.

## Entry Point

- `IXveExtension`
  - `Task ActivateAsync(ExtensionContext context, CancellationToken ct)`

### `ExtensionContext` notable services

- `Commands` (`ICommands`)
- `CommandMetadata` (`ICommandMetadataRegistry`)
- `Contributions` (`IExtensionContributionRegistry`)
- `Designer` (`IDesignerHost`)
- `Workspace` (`IWorkspace`)
- `WorkspaceModel` (`IWorkspaceModel`)
- `WorkspaceInfo` (`IWorkspaceInfo`)
- `WorkspaceHost` (`IWorkspaceHost`)
- `Window` (`IWindow`)
- `DialogHost` (`IDialogHost`)
- `Views` (`IViews`)
- `LanguageServices` (`IExtensionLanguageServices`)
- `Navigation` (`ILanguageNavigationService`)
- `NavigationHistory` (`INavigationHistoryService`)
- `AnimationEditor` (`IAnimationEditorHost`)
- `Collaboration` (`ICollaborationHost`)
- `CollaborationPanel` (`ICollaborationPanelHost`)
- `DebugSettings` (`IDebugSettingsHost`)
- `LspSettings` (`ILspSettingsHost`)
- `Editor` (`IEditorServices`)
- `Diagnostics` (`IDiagnosticsService`)
- `Terminal` (`ITerminalBridge`)
- `Permissions` (`IExtensionPermissions`)
- `ViewHost` (`IExtensionViewHost`)
- `Settings` (`ISettings`)
- `Storage` (`IExtensionStorage`)
- `Logger` (`IExtensionLogger`)
- `PropertyEditors` (`IPropertyEditorRegistry`)

## Core Services

- `ICommands`
  - `Register(string commandId, Func<CommandContext, Task> handler)`
  - `ExecuteAsync(string commandId, IReadOnlyList<object?>? args, CancellationToken ct)`
  - `GetCommandsAsync(CancellationToken ct)`

### Command arguments convention

- Commands can receive positional arguments via `CommandContext.Arguments`.
- The `toolbox.insertSelected` command now expects:
  - arg0: `typeName` (string, required)
  - arg1: `xmlNamespace` (string, required)
  - arg2: `parentNodeId` (string GUID, optional)

### Toolbox catalog settings

- The toolbox panel reads `Settings["toolbox.catalog"]` and supports:
  - typed: `List<ToolboxCatalogEntry>`
  - JSON: serialized array of `ToolboxCatalogEntry`
- `ToolboxCatalogEntry` fields:
  - `displayName` (string, required)
  - `commandId` (string, required, default: `toolbox.insertSelected`)
  - `commandArguments` (string array, optional)
  - `typeName` (string, optional; used to derive `commandArguments` when missing)
  - `xmlNamespace` (string, optional; used to derive `commandArguments` when missing)
  - `parentNodeId` (string GUID, optional; used to derive `commandArguments` when missing)

- `ICommandMetadataRegistry`
  - `Register(string commandId, CommandMetadata metadata)`
  - `TryGet(string commandId, out CommandMetadata metadata)`
  - `GetAll()`

- `IWorkspace`
  - `FindFilesAsync(string includeGlob, string? excludeGlob, CancellationToken ct)`
  - `ReadFileAsync(string path, CancellationToken ct)`
  - `WriteFileAsync(string path, byte[] content, CancellationToken ct)`
  - `CreateFileSystemWatcher(string glob)`
  - `ConfigurationChanged` event

- `IWindow`
  - `ShowInformationMessageAsync(string message, CancellationToken ct)`
  - `ShowWarningMessageAsync(string message, CancellationToken ct)`
  - `ShowErrorMessageAsync(string message, CancellationToken ct)`
  - `ShowInputBoxAsync(InputBoxOptions options, CancellationToken ct)`
  - `ShowQuickPickAsync(IReadOnlyList<QuickPickItem> items, QuickPickOptions options, CancellationToken ct)`
  - `CreateOutputChannel(string name)`
  - `GetOutputChannelsAsync(CancellationToken ct)`
  - `OutputChannelCreated` event
  - `OutputChannelRemoved` event
  - `OutputChannelMessage` event
  - `OutputChannelCleared` event
  - `CreateStatusBarItem(StatusBarAlignment alignment, int priority)`

- `IExtensionPermissions`
  - `Declare(IReadOnlyList<ExtensionCapabilityDeclaration> capabilities)`
  - `RequestAsync(string capabilityId, CancellationToken ct)`
  - `GetRememberedAsync(CancellationToken ct)`
  - `ClearRememberedAsync(string? capabilityId, CancellationToken ct)`
  - `AccessAudited` event (`ExtensionPermissionAuditEventArgs`)
  - `Changed` event (`ExtensionPermissionChangedEventArgs`)

### Permission model

- Extensions must declare capabilities before requesting them.
- Requests are resolved with a runtime quick-pick prompt:
  - `Allow once`
  - `Always allow`
  - `Deny once`
  - `Always deny`
- Remembered decisions are stored per extension in user settings under:
  - `extensions.permissions.<extensionId>`
- Every request raises an audit event that includes source (`Prompt`, `Remembered`, `Undeclared`, `Dismissed`) and decision metadata.

- `ITerminalBridge`
  - `CreateAsync(TerminalCreateRequest request, CancellationToken ct)`
  - `SendTextAsync(Guid terminalId, string text, CancellationToken ct)`
  - `GetTerminalsAsync(CancellationToken ct)`
  - `GetActiveTerminalIdAsync(CancellationToken ct)`
  - `CloseAsync(Guid terminalId, CancellationToken ct)`
  - `RunTaskAsync(TaskExecutionRequest request, CancellationToken ct)`
  - `TerminalCreated` event (`TerminalChangedEventArgs`)
  - `TerminalClosed` event (`TerminalChangedEventArgs`)
  - `ActiveTerminalChanged` event (`ActiveTerminalChangedEventArgs`)
  - `TerminalOutput` event (`TerminalOutputEventArgs`)
  - `TerminalExited` event (`TerminalExitEventArgs`)
  - `TerminalDimensionsChanged` event (`TerminalDimensionsChangedEventArgs`)

### Task execution

- `TaskExecutionRequest` accepts:
  - `taskId`, `command`, `arguments`, `workingDirectory`, `problemMatchers`
- `TaskProblemMatcher` supports regex capture groups for:
  - file path, line, column, message
- `RunTaskAsync` returns `TaskExecutionResult` with:
  - `exitCode`, `output` lines, and parsed `problems` (`TaskProblemMatch`)

- `IViews`
  - `RegisterTreeDataProvider<T>(string viewId, ITreeDataProvider<T> provider)`
  - `RegisterWebviewViewProvider(string viewId, IWebviewViewProvider provider)`
  - `RegisterCustomViewProvider(string viewId, ICustomViewProvider provider)`

- `IDiagnosticsService`
  - `GetDiagnosticsAsync(string? filePath, CancellationToken ct)`
  - `GetDiagnosticsAsync(DiagnosticsQuery query, CancellationToken ct)`
  - `GetDiagnosticsSnapshotAsync(DiagnosticsQuery query, CancellationToken ct)`
  - `GetChannelsAsync(CancellationToken ct)`
  - `ChannelsChanged` event
  - `DiagnosticsChannelPublished` event
  - `DiagnosticsSnapshotPublished` event
  - `DiagnosticsPublished` event
  - `DiagnosticsChanged` event

- `IDesignerHost`
  - `ActiveDocumentPath`
  - `ActiveDocumentChanged` event
  - `SelectionChanged` event
  - `GetSelectedNodesAsync(CancellationToken ct)` (shell-backed active selection)
  - `GetVisualTreeAsync(CancellationToken ct)` (shell-backed active AST snapshot)
  - `GetLogicalTreeAsync(CancellationToken ct)` (shell-backed active AST snapshot)
  - `GetPropertiesAsync(string nodeId, CancellationToken ct)`
  - `GetEventsAsync(string nodeId, CancellationToken ct)`
  - `SetPropertyAsync(string nodeId, string propertyName, string? value, CancellationToken ct)`
  - `InsertElementAsync(string typeName, string xmlNamespace, string? parentNodeId, CancellationToken ct)`
  - `DeleteNodeAsync(string nodeId, CancellationToken ct)`
  - `WrapNodeAsync(string nodeId, string wrapperTypeName, string wrapperXmlNamespace, CancellationToken ct)`
  - `SelectNodeAsync(string nodeId, CancellationToken ct)`
  - `RevealNodeAsync(string nodeId, CancellationToken ct)`
  - `BeginTransaction(string name)`

- `IAnimationEditorHost`
  - `ViewModel` (host-owned animation editor view model)
  - `BeginTransaction(string name)`
  - `RefreshPreviewAsync(CancellationToken ct)`

- `ICollaborationHost`
  - `IsSessionActive`, `SessionId`, `StatusMessage`
  - `GetParticipants()`
  - `SessionChanged` event
  - `ParticipantsChanged` event
  - `StartSessionAsync(CancellationToken ct)`
  - `JoinSessionAsync(string sessionId, CancellationToken ct)`
  - `LeaveSessionAsync(CancellationToken ct)`
  - `CreateShareLinkAsync(CancellationToken ct)`
  - `InviteAsync(string invitee, CancellationToken ct)`

- `ICollaborationPanelHost`
  - `ViewModel` (host-owned collaboration panel view model)

- `IDebugSettingsHost`
  - `GetState()`
  - `SetAdapterPathAsync(string adapterPath, CancellationToken ct)`
  - `SetAutoDownloadToolsAsync(bool autoDownloadTools, CancellationToken ct)`
  - `DownloadNetcoredbgAsync(CancellationToken ct)`
  - `Changed` event (`DebugSettingsChangedEventArgs`)

- `ILspSettingsHost`
  - `SettingsPath`
  - `LoadServersAsync(CancellationToken ct)`
  - `SaveServersAsync(IReadOnlyList<LspServerSettings> servers, CancellationToken ct)`
  - `Changed` event (`LspSettingsChangedEventArgs`)

- `DesignerPropertyInfo`
  - `Name`, `PropertyType`, `Value`, `IsReadOnly`
  - `Category`, `Description`, `DefaultValue`, `IsAttached`, `OwnerType`, `EnumOptions`

- `DesignerEventInfo`
  - `Name`, `HandlerName`, `Description`

- `IWorkspaceModel`
  - `HasWorkspace`
  - `WorkspacePath`
  - `Changed` event
  - `GetProjectsAsync(CancellationToken ct)`
  - `LoadAsync(CancellationToken ct)`
  - `RestoreAsync(CancellationToken ct)`
  - `BuildAsync(CancellationToken ct)`
  - `RebuildAsync(CancellationToken ct)`
  - `CleanAsync(CancellationToken ct)`

- `ISettings`
  - `Get<T>(string section, T? defaultValue = default)`
  - `UpdateAsync(string section, object? value, SettingsTarget target, CancellationToken ct)`
  - `RegisterSchema(SettingsSectionSchema schema)`
  - `GetSchemas()`
  - `TryGetSchema(string section, out SettingsSectionSchema schema)`
  - `Validate(string section, object? value)`
  - `SubscribeSection<T>(string section, Action<SettingsSectionChangedEventArgs<T>> handler)`
  - `SectionChanged` event (`SettingsSectionChangedEventArgs`)

- `IExtensionStorage`
  - `GetAsync<T>(string key, CancellationToken ct)`
  - `SetAsync<T>(string key, T value, CancellationToken ct)`
  - `RemoveAsync(string key, CancellationToken ct)`

- `IExtensionLogger`
  - `Info(string message)`
  - `Warn(string message)`
  - `Error(string message, Exception? exception = null)`

- `IExtensionContributionRegistry`
  - `RegisterPropertyEditors(string extensionId, IReadOnlyList<ExtensionPropertyEditorContribution> editors)`
  - `PropertyEditors` collection

- Menu locations (`ExtensionMenuLocations`)
  - `menu.file`
  - `menu.file.new`
  - `menu.edit`
  - `menu.view`
  - `menu.tools`
  - `menu.tools.workspace`
  - `menu.extensions`

- `IExtensionViewHost`
  - `VisibilityChanged` event (`ExtensionViewVisibilityChangedEventArgs`)
  - `FocusChanged` event (`ExtensionViewFocusChangedEventArgs`)
  - `ShowAsync(string viewId, CancellationToken ct)`
  - `ToggleAsync(string viewId, CancellationToken ct)`
  - `IsVisibleAsync(string viewId, CancellationToken ct)`
  - `ActivateAsync(string viewId, CancellationToken ct)`

### ExtensionViewContribution

- `ViewId` (string, required)
- `Title` (string, required)
- `Type` (`ExtensionViewType`)
- `Location` (`ExtensionViewLocation`)
- `Priority` (int)
- `ActivateByDefault` (bool, default `false`)
- `ContainerId` (optional string; custom dock container id)
- `PersistDockState` (bool, default `true`; persistence hint for layout save/restore)

- `IPropertyEditorRegistry`
  - `Register(PropertyEditorDescriptor descriptor)`
  - `TryGet(string propertyType, out PropertyEditorDescriptor descriptor)`
  - `GetAll()`

### PropertyEditorDescriptor

- `PropertyType` (string, required)
- `Kind` (`PropertyEditorKind`)
- `EnumOptions` (optional string list)
- `BrushPresets` (optional string list)

### ExtensionPropertyEditorContribution

- `PropertyType` (string, required)
- `Kind` (`PropertyEditorKind`)
- `EnumOptions` (optional string list)
- `BrushPresets` (optional string list)

### PropertyEditorKind

- `Text`
- `Boolean`
- `Number`
- `Enum`
- `Brush`

## Language Services

- `IExtensionLanguageServices`
  - Register completion, hover, definition, references, signature help,
    code actions, formatting, diagnostics, and document sync providers.

## Editor Services

- `IEditorServices`
  - `ActiveDocument` / `GetOpenDocuments()`
  - `OpenDocumentAsync(string filePath, CancellationToken ct)`
  - `OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)`
  - `OpenLocationAsync(LanguageLocation location, CancellationToken ct)`
  - `ActiveDocumentChanged` event

- `ILanguageNavigationService`
  - `FindDefinitionsAsync(LanguagePositionContext context, CancellationToken ct)`
  - `FindReferencesAsync(LanguagePositionContext context, CancellationToken ct)`
  - `FindImplementationsAsync(LanguagePositionContext context, CancellationToken ct)`
  - `GetWorkspaceSymbolsAsync(LanguageSymbolQuery query, CancellationToken ct)`
  - `PrepareRenameAsync(LanguagePositionContext context, CancellationToken ct)`
  - `RenameAsync(LanguageRenameContext context, CancellationToken ct)`
  - `GetCodeActionsAsync(LanguageCodeActionContext context, CancellationToken ct)`
  - `ResolveCodeActionAsync(LanguageCodeAction action, CancellationToken ct)`
  - `ApplyCodeActionAsync(LanguageCodeAction action, CancellationToken ct)`

- `INavigationHistoryService`
  - `CanNavigateBack` / `CanNavigateForward`
  - `HistoryChanged` event
  - `NavigateBackAsync(CancellationToken ct)`
  - `NavigateForwardAsync(CancellationToken ct)`

- `IEditorDocument`
  - `FilePath`, `LanguageId`, `CaretOffset`
  - `GetTextAsync(CancellationToken ct)`
  - `ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)`
  - `Changed` event
