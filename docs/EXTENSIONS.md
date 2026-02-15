# XamlVisualEditor Extensions

This document tracks extension migration status and extension packaging/installation.

## Migration Status

### Extension-contributed tool windows

- `toolbox.panel`: migrated (`extensions/XamlVisualEditor.ToolboxExtension`)
- `propertyEditor.panel`: migrated (`extensions/XamlVisualEditor.PropertyEditorExtension`)
- `visualTree.panel` + `logicalTree.panel`: migrated (`extensions/XamlVisualEditor.TreeInspectorExtension`)
- `output.panel` + `problems.panel`: migrated (`extensions/XamlVisualEditor.OutputExtension`)
- `references.panel` + navigation commands: migrated (`extensions/XamlVisualEditor.NavigationExtension`)
- `animationEditor.panel`: migrated (`extensions/XamlVisualEditor.AnimationEditorExtension`)
- `collaboration.panel`: migrated (`extensions/XamlVisualEditor.CollaborationExtension`)
- `debugSettings.panel`: migrated (`extensions/XamlVisualEditor.DebugSettingsExtension`)
- `lspSettings.panel`: migrated (`extensions/XamlVisualEditor.LspSettingsExtension`)

### Remaining migration focus

- Keep reducing shell-owned command handlers in `MainWindowViewModel`.
- Continue replacing host adapters that expose raw host ViewModels with typed extension APIs.
- Keep hardening terminal/task and settings schema APIs in `XamlVisualEditor.Extensions`.

### Related docs

- API reference: `docs/EXTENSION-API.md`
- Internal migration guide: `docs/EXTENSION-MIGRATION-GUIDE.md`
- Shell command audit: `docs/SHELL-COMMAND-AUDIT.md`

## Packaging (NuGet)

Extensions are distributed as NuGet packages (`.nupkg`). Each package must include:

- `xve.extension.json` at the package root
- A .NET assembly implementing `IXveExtension`

The Extensions Manager installs `.nupkg` files and persists enabled/disabled state.

## Installing an Extension

1. Build or download a `.nupkg`.
2. Open `View > Extensions Manager`.
3. Click `Install...` and choose the package.
4. Toggle `Enabled` to activate/deactivate.

## Manifest Overview

The manifest is `xve.extension.json` at package root. Example:

```json
{
  "name": "hello-extension",
  "displayName": "Hello Extension",
  "publisher": "sample",
  "version": "0.1.0",
  "engines": { "xve": "^0.1.0" },
  "main": "lib/net10.0/HelloExtension.dll",
  "activationEvents": ["onStartupFinished", "onCommand:hello.showMessage"],
  "contributes": {
    "commands": [{ "command": "hello.showMessage", "title": "Hello: Show Message" }],
    "menus": {
      "commandPalette": [{ "command": "hello.showMessage" }]
    }
  }
}
```

## Samples

- `tools/ExtensionSamples/HelloExtension`
- `tools/ExtensionSamples/LspExtension`

Build sample package:

```bash
dotnet pack tools/ExtensionSamples/HelloExtension/HelloExtension.csproj -c Release
```
