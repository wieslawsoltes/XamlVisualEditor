# XamlVisualEditor Extensions

This guide explains how to author and install native .NET extensions for XamlVisualEditor.

## Packaging (NuGet)

Extensions are distributed as NuGet packages (.nupkg). Each package must include:
- `xve.extension.json` at the package root
- A .NET assembly that implements `IXveExtension`

The Extensions Manager installs `.nupkg` files and tracks enablement state.

## Installing an Extension

1. Build or download a `.nupkg` package.
2. Open **View > Extensions Manager**.
3. Click **Install...** and select the `.nupkg` file.
4. Toggle the **Enabled** checkbox to activate the extension.

## Manifest Overview

The manifest is `xve.extension.json` at the package root. Example:

```json
{
  "name": "hello-extension",
  "displayName": "Hello Extension",
  "publisher": "sample",
  "version": "0.1.0",
  "engines": { "xve": "^0.1.0" },
  "main": "lib/net10.0/HelloExtension.dll",
  "activationEvents": [
    "onStartupFinished",
    "onCommand:hello.showMessage"
  ],
  "contributes": {
    "commands": [
      { "command": "hello.showMessage", "title": "Hello: Show Message" }
    ],
    "menus": {
      "commandPalette": [ { "command": "hello.showMessage" } ]
    },
    "views": {
      "explorer": [ { "id": "hello.view", "name": "Hello View" } ]
    }
  }
}
```

## Samples

See the sample extension in:
- `tools/ExtensionSamples/HelloExtension`
- `tools/ExtensionSamples/LspExtension`

Build and pack:

```bash
# from repo root

dotnet pack tools/ExtensionSamples/HelloExtension/HelloExtension.csproj -c Release
```

Then install the generated `.nupkg` using the Extensions Manager.
