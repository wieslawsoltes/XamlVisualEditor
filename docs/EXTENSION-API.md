# Extension SDK API Reference

This is the current native .NET extension API surface. All types live in the
`XamlVisualEditor.Extensions` namespace.

## Entry Point

- `IXveExtension`
  - `Task ActivateAsync(ExtensionContext context, CancellationToken ct)`

## Core Services

- `ICommands`
  - `Register(string commandId, Func<CommandContext, Task> handler)`
  - `ExecuteAsync(string commandId, IReadOnlyList<object?>? args, CancellationToken ct)`
  - `GetCommandsAsync(CancellationToken ct)`

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
  - `CreateStatusBarItem(StatusBarAlignment alignment, int priority)`

- `IViews`
  - `RegisterTreeDataProvider<T>(string viewId, ITreeDataProvider<T> provider)`
  - `RegisterWebviewViewProvider(string viewId, IWebviewViewProvider provider)`

- `ISettings`
  - `Get<T>(string section, T? defaultValue = default)`
  - `UpdateAsync(string section, object? value, SettingsTarget target, CancellationToken ct)`

- `IExtensionStorage`
  - `GetAsync<T>(string key, CancellationToken ct)`
  - `SetAsync<T>(string key, T value, CancellationToken ct)`
  - `RemoveAsync(string key, CancellationToken ct)`

- `IExtensionLogger`
  - `Info(string message)`
  - `Warn(string message)`
  - `Error(string message, Exception? exception = null)`

## Language Services

- `IExtensionLanguageServices`
  - Register completion, hover, definition, references, signature help,
    code actions, formatting, diagnostics, and document sync providers.

## Editor Services

- `IEditorServices`
  - `ActiveDocument` / `GetOpenDocuments()`
  - `ActiveDocumentChanged` event

- `IEditorDocument`
  - `FilePath`, `LanguageId`, `CaretOffset`
  - `GetTextAsync(CancellationToken ct)`
  - `ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)`
  - `Changed` event
