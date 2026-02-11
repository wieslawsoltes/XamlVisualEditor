# Extension Host JSON-RPC Spike

Small proof-of-concept that simulates a VS Code-style extension host calling into the app over JSON-RPC using stdio.

## What It Does

- Extension process sends JSON-RPC requests to the host.
- Host processes requests and returns JSON-RPC responses.
- Methods included:
  - `xve.commands.register`
  - `xve.workspace.getConfiguration`
  - `xve.window.showInformationMessage`

## Build

```
dotnet build tools/ExtensionHostSpike/ExtensionHostSpike.Extension/ExtensionHostSpike.Extension.csproj
dotnet build tools/ExtensionHostSpike/ExtensionHostSpike.Host/ExtensionHostSpike.Host.csproj
```

## Run

```
dotnet run --project tools/ExtensionHostSpike/ExtensionHostSpike.Host/ExtensionHostSpike.Host.csproj -- \
  --extension-path tools/ExtensionHostSpike/ExtensionHostSpike.Extension/bin/Debug/net10.0/ExtensionHostSpike.Extension
```

On macOS, the extension binary is a file without an extension. If the default build output path differs, pass the correct path via `--extension-path`.
