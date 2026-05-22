# XamlVisualEditor

[![Build](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/build.yml)
[![Docs](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/docs.yml/badge.svg)](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/docs.yml)
[![Release](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/release.yml/badge.svg)](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/release.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

XamlVisualEditor is an extensible Avalonia-based visual IDE for editing, previewing,
inspecting, debugging, and automating XAML applications. It combines an
AvaloniaEdit code surface, a live design surface, Dock-based tool windows,
language services, debugger adapters, terminal integration, collaboration
services, and a native .NET extension model.

The repository targets .NET 10 and is built around strict MVVM, ReactiveUI,
compiled Avalonia bindings, Dock for Avalonia, Xaml.Behaviors, AvaloniaEdit,
ProDataGrid, Microsoft.Extensions.DependencyInjection, and xUnit/Avalonia
Headless tests.

## Highlights

- **Live XAML editing**: parse, serialize, and round-trip XAML through XamlX-backed
  AST services with minimal text changes.
- **Visual designer**: instantiate Avalonia controls from AST nodes, map visuals
  back to source nodes, and support drag/drop, selection, resize handles, rulers,
  grids, margins, and padding adorners.
- **Code editor**: AvaloniaEdit integration with syntax highlighting, completion,
  semantic tokens, diagnostics, and execution-line rendering.
- **Workspace tooling**: open workspaces, build/clean/rebuild projects, restore
  state, select startup projects, and integrate file-system backed extension APIs.
- **Language services**: C# and XAML completion/diagnostics plus an LSP router for
  external language servers.
- **Extension-first shell**: built-in and packaged extensions contribute commands,
  menus, toolbar actions, views, language servers, property editors, and panels.
- **Debugging**: DAP and .NET SDK debugger integrations, launch validation,
  breakpoint/call-stack/locals/watch panels, and debug settings tooling.
- **Developer panels**: solution explorer, file explorer, toolbox, property editor,
  output/problems, references, tree inspectors, animation editor, collaboration,
  ACP, MCP, IDE bridge, LSP settings, terminal, and Git views.
- **Collaboration and agents**: ACP client services, OAuth/device-flow support,
  permissions, profile storage, and ProEdit-backed collaborative AST operations.
- **Terminal and Git**: managed terminal emulator/PTY support and Git status/diff
  parsing surfaced through extension panels.
- **Test coverage**: unit, integration, performance, and Avalonia Headless UI tests
  cover parsing, services, extension plumbing, UI flows, terminal behavior, LSP,
  ACP, previewer protocols, and workspace workflows.

## Repository Layout

| Path | Purpose |
| --- | --- |
| `src/XamlVisualEditor.App` | Avalonia application entry point, composition root, views, and resources. |
| `src/XamlVisualEditor.Shell.ViewModels` | Main window, documents, Dock factories, panels, and shell orchestration ViewModels. |
| `src/XamlVisualEditor.Extensions` | Native extension SDK contracts, host services, package loading, and contribution registries. |
| `src/XamlVisualEditor.Xaml.*` | XAML parsing, AST, serialization, intellisense, and language server components. |
| `src/XamlVisualEditor.Designer.*` | Designer abstractions, rendering, adorners, drag/drop, and previewer host. |
| `src/XamlVisualEditor.CodeEditor` | AvaloniaEdit integration and editor rendering helpers. |
| `src/XamlVisualEditor.Workspace` | MSBuild workspace loading, template discovery, assembly resolution, and CLI helpers. |
| `src/XamlVisualEditor.Lsp` | JSON-RPC/LSP client transport, routing, diagnostics, and test hooks. |
| `src/XamlVisualEditor.Acp` | Agent Client Protocol client, permissions, profiles, secrets, and process hosting. |
| `src/XamlVisualEditor.Terminal*` | Terminal emulator core, platform PTY providers, and Avalonia terminal view. |
| `extensions/` | Built-in extension projects for panels, commands, debugging, templates, Git, MCP, ACP, and compatibility hosts. |
| `tests/` | Unit, integration, performance, and Avalonia Headless UI test projects. |
| `tools/` | Extension samples, LSP harness, IDE bridge CLI, and extension host spike. |
| `docs/` | Internal architecture, extension migration, and API notes. |
| `site/` | Lunet documentation site published by GitHub Pages. |

## Build

Prerequisites:

- .NET SDK 10.0.x
- Git submodules initialized when working with the full workspace

```bash
git submodule update --init --recursive
dotnet restore XamlVisualEditor.slnx
dotnet build XamlVisualEditor.slnx -c Release --no-restore
```

Run the main test projects:

```bash
dotnet test tests/XamlVisualEditor.Tests.Unit/XamlVisualEditor.Tests.Unit.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.Integration/XamlVisualEditor.Tests.Integration.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.UI/XamlVisualEditor.Tests.UI.csproj -c Release
```

Run the application:

```bash
dotnet run --project src/XamlVisualEditor.App/XamlVisualEditor.App.csproj
```

## Documentation

The documentation site is built with Lunet from `site/`.

```bash
./build-docs.sh
cd site
dotnet tool run lunet serve
```

The site covers product features, architecture, extension development,
debugging, collaboration, language services, testing, CI, and release guidance.
Existing internal notes remain in `docs/` and are linked from the site reference
section.

## Extension Model

Extensions are packaged as NuGet packages with `xve.extension.json` at the package
root and a .NET assembly implementing `IXveExtension`. The extension host exposes
typed services for commands, views, workspace access, diagnostics, editor
operations, navigation, terminal access, settings, storage, logging, dialogs,
permissions, and panel hosts.

Sample extensions are available in `tools/ExtensionSamples/HelloExtension` and
`tools/ExtensionSamples/LspExtension`.

```bash
dotnet pack tools/ExtensionSamples/HelloExtension/HelloExtension.csproj -c Release
```

## NuGet Packages

Reusable libraries and built-in extensions carry repository, license, readme,
icon, Source Link, and symbol package metadata. Release builds pack the solution
into `.nupkg` and `.snupkg` artifacts, publish them to NuGet when
`NUGET_API_KEY` is configured, and attach the packages to the GitHub release.

## Contributing

Follow the repository engineering rules in `AGENTS.md`: strict SOLID and MVVM,
passive views, ReactiveUI ViewModels, compiled Avalonia bindings, Xaml.Behaviors
for UI input, Dock model state in ViewModels, ProDataGrid for tabular/tree data,
Microsoft.Extensions.DependencyInjection from a single composition root, and tests
for production behavior.

Before opening a pull request, build the solution, run the relevant test projects,
and build the Lunet documentation site.
