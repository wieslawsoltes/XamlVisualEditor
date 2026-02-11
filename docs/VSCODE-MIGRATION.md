# VS Code Extension Migration Guide

This guide outlines how to adapt a VS Code extension to the XamlVisualEditor
native extension model (Path B). A compatibility layer (Path A) is out of scope
for now.

## Packaging Differences

- VS Code: `package.json` + `.vsix`
- XamlVisualEditor: `xve.extension.json` + NuGet `.nupkg`

The manifest file `xve.extension.json` lives at the package root. The main
assembly is referenced by the `main` field, for example:

```json
{
  "main": "lib/net10.0/MyExtension.dll"
}
```

## Manifest Mapping

| VS Code | XamlVisualEditor |
| --- | --- |
| `package.json` | `xve.extension.json` |
| `activationEvents` | `activationEvents` |
| `contributes.commands` | `contributes.commands` |
| `contributes.menus` | `contributes.menus` |
| `contributes.views` | `contributes.views` |
| `contributes.languageServers` | `contributes.languageServers` |

Notes:
- Contribution schemas match VS Code where possible.
- The host reads contributions and maps them into Avalonia MVVM constructs.

## API Differences

VS Code extensions use the `vscode` module. XamlVisualEditor extensions use the
`XamlVisualEditor.Extensions` SDK:

```csharp
public sealed class MyExtension : IXveExtension
{
    public Task ActivateAsync(ExtensionContext context, CancellationToken ct)
    {
        context.Commands.Register("my.command", _ => Task.CompletedTask);
        return Task.CompletedTask;
    }
}
```

Core services:
- `ICommands`, `IWorkspace`, `IWindow`, `IViews`
- `IExtensionLanguageServices`, `IEditorServices`

## Activation Events

Activation events match VS Code naming where feasible:
- `onStartupFinished`
- `onCommand:<commandId>`
- `onLanguage:<languageId>`
- `onView:<viewId>`

## Webviews

Webview support is planned but currently limited. Use tree views and standard
UI contributions when possible.

## Permissions

Permissions are declared in `xve.extension.json` and are deny-by-default.
Refer to the permissions plan for details.

## Migration Checklist

1. Replace `package.json` with `xve.extension.json`.
2. Port activation events and contributions.
3. Re-implement extension entry point using `IXveExtension`.
4. Package as a NuGet `.nupkg`.
5. Install via **View > Extensions Manager**.
