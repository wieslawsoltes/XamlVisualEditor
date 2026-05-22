---
title: Build and Run
description: Restore, build, test, and run the XamlVisualEditor application from source.
---

# Build and Run

## Restore

Initialize submodules before restoring the solution:

```bash
git submodule update --init --recursive
dotnet restore XamlVisualEditor.slnx
```

## Build

Build the full solution in Release mode:

```bash
dotnet build XamlVisualEditor.slnx -c Release --no-restore
```

The application project copies its external previewer host and VS Code
compatibility host assets during build.

## Run

```bash
dotnet run --project src/XamlVisualEditor.App/XamlVisualEditor.App.csproj
```

You can pass a workspace path to the app. The startup code resolves the argument
and opens it after the main window is ready.

## Test

The test suite is split by scope:

| Project | Purpose |
| --- | --- |
| `XamlVisualEditor.Tests.Unit` | ViewModels, services, AST, parsing, serialization, language services, terminal, Git parsers, and extension plumbing. |
| `XamlVisualEditor.Tests.Integration` | ACP, DAP, LSP diagnostics, previewer protocol, extension host spike, and XAML round trips. |
| `XamlVisualEditor.Tests.UI` | Avalonia Headless shell, panel, terminal, command menu, and view tests. |
| `XamlVisualEditor.Tests.Performance` | Large XAML parse, serialization, sync, and intellisense thresholds. |

