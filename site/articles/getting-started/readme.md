---
title: Getting Started
description: Build XamlVisualEditor from source, run the Avalonia app, and open a XAML workspace.
---

# Getting Started

XamlVisualEditor is built from source as a .NET 10 solution. The main application
is `src/XamlVisualEditor.App/XamlVisualEditor.App.csproj`; tests live under
`tests/`; docs are generated from `site/` with Lunet.

## Prerequisites

- .NET SDK 10.0.x.
- Git with submodule support.
- macOS, Windows, or Linux for the core build; platform-specific terminal and
  debugger behavior depends on the host operating system.

## Quick commands

```bash
git submodule update --init --recursive
dotnet restore XamlVisualEditor.slnx
dotnet build XamlVisualEditor.slnx -c Release --no-restore
dotnet run --project src/XamlVisualEditor.App/XamlVisualEditor.App.csproj
```

Run focused test projects:

```bash
dotnet test tests/XamlVisualEditor.Tests.Unit/XamlVisualEditor.Tests.Unit.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.Integration/XamlVisualEditor.Tests.Integration.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.UI/XamlVisualEditor.Tests.UI.csproj -c Release
```

Build the documentation:

```bash
./build-docs.sh
```

## Next steps

- [Build and Run](build-and-run.md)
- [First Workspace](first-workspace.md)
- [Feature Tour](../guides/feature-tour.md)

