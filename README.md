# XamlVisualEditor

[![Build](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/build.yml)
[![Docs](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/docs.yml/badge.svg)](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/docs.yml)
[![Release](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/release.yml/badge.svg)](https://github.com/wieslawsoltes/XamlVisualEditor/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/wieslawsoltes/XamlVisualEditor?sort=semver)](https://github.com/wieslawsoltes/XamlVisualEditor/releases/latest)
[![License](https://img.shields.io/github/license/wieslawsoltes/XamlVisualEditor)](LICENSE)

XamlVisualEditor is an extensible, cross-platform visual IDE for Avalonia XAML.
It combines a live designer, AvaloniaEdit code editor, language services,
debugging, terminal and Git tooling, collaboration services, and a native .NET
extension model in one Dock-based workspace.

[Download](https://github.com/wieslawsoltes/XamlVisualEditor/releases/latest) ·
[Documentation](https://wieslawsoltes.github.io/XamlVisualEditor/) ·
[Extension API](docs/EXTENSION-API.md) ·
[Changelog](CHANGELOG.md) ·
[Issues](https://github.com/wieslawsoltes/XamlVisualEditor/issues) ·
[Security](SECURITY.md)

## Highlights

- Live XAML parsing, AST synchronization, minimal-edit serialization, preview,
  selection, resizing, drag-and-drop, rulers, grids, and layout adorners.
- AvaloniaEdit code surfaces with TextMate highlighting, completion, semantic
  tokens, diagnostics, navigation, and execution-line rendering.
- C# and XAML language services plus an LSP router for external language servers.
- DAP and .NET SDK debugging with breakpoints, call stack, locals, watches, and
  configurable debugger tooling.
- Extensible commands, menus, toolbar actions, panels, views, property editors,
  language servers, settings, storage, dialogs, and permissions.
- Solution and file explorers, toolbox, property editor, output/problems,
  references, tree inspectors, animation editor, terminal, Git, ACP, MCP,
  collaboration, and IDE-bridge panels.
- Strict MVVM and ReactiveUI architecture with compiled Avalonia bindings,
  Xaml.Behaviors input routing, dependency injection, and source-generated
  reactive properties.
- Unit, integration, and Avalonia Headless UI suites run on Linux, Windows, and
  macOS in CI, with a dedicated Linux performance-test job.

## Download and run

Download the archive for your operating system and CPU from the
[latest release](https://github.com/wieslawsoltes/XamlVisualEditor/releases/latest).

| Platform | x64 | Arm64 |
| --- | --- | --- |
| Linux | `XamlVisualEditor-<version>-linux-x64.zip` | `XamlVisualEditor-<version>-linux-arm64.zip` |
| Windows | `XamlVisualEditor-<version>-win-x64.zip` | `XamlVisualEditor-<version>-win-arm64.zip` |
| macOS | `XamlVisualEditor-<version>-macos-x64.zip` | `XamlVisualEditor-<version>-macos-arm64.zip` |

Release archives are framework-dependent and require the [.NET 10
runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Building or loading
.NET workspaces also requires a compatible .NET SDK. Extract the archive and run
`XamlVisualEditor.App` on Linux/macOS or `XamlVisualEditor.App.exe` on Windows.

The current release archives are not code-signed or notarized, so the operating
system may show a first-run security prompt. Release assets include
`SHA256SUMS`; verify the archive before running it when your environment requires
an integrity check. Each archive also contains the project README, changelog,
and MIT license.

## Build from source

Prerequisites:

- Git
- .NET 10 SDK; `global.json` accepts the latest installed .NET 10 feature band
- The repository submodules

```bash
git clone --recurse-submodules https://github.com/wieslawsoltes/XamlVisualEditor.git
cd XamlVisualEditor
dotnet restore XamlVisualEditor.slnx
dotnet build XamlVisualEditor.slnx -c Release --no-restore
dotnet run --project src/XamlVisualEditor.App/XamlVisualEditor.App.csproj -c Release
```

If the repository was cloned without submodules, initialize them with:

```bash
git submodule update --init --recursive
```

## Test

```bash
dotnet test tests/XamlVisualEditor.Tests.Unit/XamlVisualEditor.Tests.Unit.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.Integration/XamlVisualEditor.Tests.Integration.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.UI/XamlVisualEditor.Tests.UI.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.Performance/XamlVisualEditor.Tests.Performance.csproj -c Release
```

## NuGet packages

The release publishes reusable libraries and built-in extensions to
[NuGet.org](https://www.nuget.org/profiles/wieslawsoltes). Packages include MIT
license metadata, the repository README and icon, Source Link, deterministic
build metadata, and `.snupkg` symbols. Install a package with:

```bash
dotnet add package XamlVisualEditor.Core
```

### Core, shell, and services

| Package | Version | Purpose |
| --- | --- | --- |
| [XamlVisualEditor.Core](https://www.nuget.org/packages/XamlVisualEditor.Core) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Core.svg)](https://www.nuget.org/packages/XamlVisualEditor.Core) | Shared primitives, interfaces, enums, and contracts. |
| [XamlVisualEditor.Extensions](https://www.nuget.org/packages/XamlVisualEditor.Extensions) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Extensions.svg)](https://www.nuget.org/packages/XamlVisualEditor.Extensions) | Extension SDK, manifests, hosting, permissions, and contribution registries. |
| [XamlVisualEditor.Shell](https://www.nuget.org/packages/XamlVisualEditor.Shell) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Shell.svg)](https://www.nuget.org/packages/XamlVisualEditor.Shell) | Dock factory and IDE-style layout. |
| [XamlVisualEditor.Shell.ViewModels](https://www.nuget.org/packages/XamlVisualEditor.Shell.ViewModels) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Shell.ViewModels.svg)](https://www.nuget.org/packages/XamlVisualEditor.Shell.ViewModels) | Documents, panels, commands, and shell orchestration. |
| [XamlVisualEditor.Workspace](https://www.nuget.org/packages/XamlVisualEditor.Workspace) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Workspace.svg)](https://www.nuget.org/packages/XamlVisualEditor.Workspace) | MSBuild workspace loading, assembly resolution, and metadata. |
| [XamlVisualEditor.Terminal](https://www.nuget.org/packages/XamlVisualEditor.Terminal) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Terminal.svg)](https://www.nuget.org/packages/XamlVisualEditor.Terminal) | Terminal emulator, PTY integration, and sessions. |
| [XamlVisualEditor.Terminal.Avalonia](https://www.nuget.org/packages/XamlVisualEditor.Terminal.Avalonia) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Terminal.Avalonia.svg)](https://www.nuget.org/packages/XamlVisualEditor.Terminal.Avalonia) | Avalonia terminal control integration. |
| [XamlVisualEditor.Collaboration](https://www.nuget.org/packages/XamlVisualEditor.Collaboration) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Collaboration.svg)](https://www.nuget.org/packages/XamlVisualEditor.Collaboration) | CRDT-backed collaborative AST operations. |
| [XamlVisualEditor.Collaboration.UI](https://www.nuget.org/packages/XamlVisualEditor.Collaboration.UI) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Collaboration.UI.svg)](https://www.nuget.org/packages/XamlVisualEditor.Collaboration.UI) | Collaboration presence and session ViewModels. |
| [XamlVisualEditor.Acp](https://www.nuget.org/packages/XamlVisualEditor.Acp) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Acp.svg)](https://www.nuget.org/packages/XamlVisualEditor.Acp) | Agent Client Protocol client and stdio transport. |
| [XamlVisualEditor.Animation](https://www.nuget.org/packages/XamlVisualEditor.Animation) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Animation.svg)](https://www.nuget.org/packages/XamlVisualEditor.Animation) | Animation domain models and AST resource writer. |
| [XamlVisualEditor.Sync](https://www.nuget.org/packages/XamlVisualEditor.Sync) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Sync.svg)](https://www.nuget.org/packages/XamlVisualEditor.Sync) | Text, AST, designer, and collaboration synchronization. |

### Designer and editing

| Package | Version | Purpose |
| --- | --- | --- |
| [XamlVisualEditor.CodeEditor](https://www.nuget.org/packages/XamlVisualEditor.CodeEditor) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.CodeEditor.svg)](https://www.nuget.org/packages/XamlVisualEditor.CodeEditor) | AvaloniaEdit integration and editor rendering. |
| [XamlVisualEditor.Designer.Core](https://www.nuget.org/packages/XamlVisualEditor.Designer.Core) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Designer.Core.svg)](https://www.nuget.org/packages/XamlVisualEditor.Designer.Core) | Design surfaces, items, selection, and hit testing. |
| [XamlVisualEditor.Designer.Rendering](https://www.nuget.org/packages/XamlVisualEditor.Designer.Rendering) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Designer.Rendering.svg)](https://www.nuget.org/packages/XamlVisualEditor.Designer.Rendering) | Live Avalonia control creation and visual-to-AST mapping. |
| [XamlVisualEditor.Designer.DragDrop](https://www.nuget.org/packages/XamlVisualEditor.Designer.DragDrop) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Designer.DragDrop.svg)](https://www.nuget.org/packages/XamlVisualEditor.Designer.DragDrop) | Toolbox, surface, and tree drag-and-drop protocols. |
| [XamlVisualEditor.Designer.Adorners](https://www.nuget.org/packages/XamlVisualEditor.Designer.Adorners) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Designer.Adorners.svg)](https://www.nuget.org/packages/XamlVisualEditor.Designer.Adorners) | Selection, resize, snap-line, and spacing adorners. |
| [XamlVisualEditor.PropertyEditor](https://www.nuget.org/packages/XamlVisualEditor.PropertyEditor) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.PropertyEditor.svg)](https://www.nuget.org/packages/XamlVisualEditor.PropertyEditor) | DataGrid-based property grid and inline editors. |
| [XamlVisualEditor.TreeView](https://www.nuget.org/packages/XamlVisualEditor.TreeView) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.TreeView.svg)](https://www.nuget.org/packages/XamlVisualEditor.TreeView) | Visual and logical tree ViewModels. |

### Language and XAML

| Package | Version | Purpose |
| --- | --- | --- |
| [XamlVisualEditor.CSharp.Language](https://www.nuget.org/packages/XamlVisualEditor.CSharp.Language) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.CSharp.Language.svg)](https://www.nuget.org/packages/XamlVisualEditor.CSharp.Language) | Roslyn C# completion, diagnostics, and navigation. |
| [XamlVisualEditor.Language](https://www.nuget.org/packages/XamlVisualEditor.Language) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Language.svg)](https://www.nuget.org/packages/XamlVisualEditor.Language) | Shared language-service registry and helpers. |
| [XamlVisualEditor.Lsp](https://www.nuget.org/packages/XamlVisualEditor.Lsp) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Lsp.svg)](https://www.nuget.org/packages/XamlVisualEditor.Lsp) | LSP client transport, routing, and diagnostics. |
| [XamlVisualEditor.Xaml.Ast](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Ast) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Xaml.Ast.svg)](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Ast) | Mutable, observable XAML AST. |
| [XamlVisualEditor.Xaml.Parsing](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Parsing) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Xaml.Parsing.svg)](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Parsing) | XamlX-backed parsing and diagnostics. |
| [XamlVisualEditor.Xaml.Serialization](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Serialization) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Xaml.Serialization.svg)](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Serialization) | Whitespace-preserving minimal-edit serialization. |
| [XamlVisualEditor.Xaml.Intellisense](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Intellisense) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Xaml.Intellisense.svg)](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Intellisense) | Schema inference, completion, and XML namespace resolution. |
| [XamlVisualEditor.Xaml.Language](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Language) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Xaml.Language.svg)](https://www.nuget.org/packages/XamlVisualEditor.Xaml.Language) | XAML completion, diagnostics, and hover services. |

### Built-in extensions

| Package | Version | Purpose |
| --- | --- | --- |
| [XamlVisualEditor.AcpExtension](https://www.nuget.org/packages/XamlVisualEditor.AcpExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.AcpExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.AcpExtension) | ACP profiles, authentication, permissions, and transcript UI. |
| [XamlVisualEditor.AnimationEditorExtension](https://www.nuget.org/packages/XamlVisualEditor.AnimationEditorExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.AnimationEditorExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.AnimationEditorExtension) | Animation editor panel. |
| [XamlVisualEditor.CollaborationExtension](https://www.nuget.org/packages/XamlVisualEditor.CollaborationExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.CollaborationExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.CollaborationExtension) | Collaboration panel. |
| [XamlVisualEditor.DebugSettingsExtension](https://www.nuget.org/packages/XamlVisualEditor.DebugSettingsExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.DebugSettingsExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.DebugSettingsExtension) | Debugger settings panel. |
| [XamlVisualEditor.Debugging.DapExtension](https://www.nuget.org/packages/XamlVisualEditor.Debugging.DapExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Debugging.DapExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.Debugging.DapExtension) | DAP-backed .NET debugger integration. |
| [XamlVisualEditor.Debugging.DotNetSdkExtension](https://www.nuget.org/packages/XamlVisualEditor.Debugging.DotNetSdkExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.Debugging.DotNetSdkExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.Debugging.DotNetSdkExtension) | .NET SDK debugger integration. |
| [XamlVisualEditor.DotNetTemplatesExtension](https://www.nuget.org/packages/XamlVisualEditor.DotNetTemplatesExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.DotNetTemplatesExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.DotNetTemplatesExtension) | .NET project and solution template wizard. |
| [XamlVisualEditor.FileExplorerExtension](https://www.nuget.org/packages/XamlVisualEditor.FileExplorerExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.FileExplorerExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.FileExplorerExtension) | File explorer panel. |
| [XamlVisualEditor.GitExtension](https://www.nuget.org/packages/XamlVisualEditor.GitExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.GitExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.GitExtension) | Git status, change, and diff panel. |
| [XamlVisualEditor.IdeBridgeExtension](https://www.nuget.org/packages/XamlVisualEditor.IdeBridgeExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.IdeBridgeExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.IdeBridgeExtension) | IDE bridge runtime and configuration panel. |
| [XamlVisualEditor.LspSettingsExtension](https://www.nuget.org/packages/XamlVisualEditor.LspSettingsExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.LspSettingsExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.LspSettingsExtension) | External language-server settings. |
| [XamlVisualEditor.McpExtension](https://www.nuget.org/packages/XamlVisualEditor.McpExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.McpExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.McpExtension) | MCP runtime and permissions panel. |
| [XamlVisualEditor.NavigationExtension](https://www.nuget.org/packages/XamlVisualEditor.NavigationExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.NavigationExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.NavigationExtension) | References and language-service navigation. |
| [XamlVisualEditor.OutputExtension](https://www.nuget.org/packages/XamlVisualEditor.OutputExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.OutputExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.OutputExtension) | Output channels and problems panel. |
| [XamlVisualEditor.PropertyEditorExtension](https://www.nuget.org/packages/XamlVisualEditor.PropertyEditorExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.PropertyEditorExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.PropertyEditorExtension) | Designer selection and property editor integration. |
| [XamlVisualEditor.SolutionExplorerExtension](https://www.nuget.org/packages/XamlVisualEditor.SolutionExplorerExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.SolutionExplorerExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.SolutionExplorerExtension) | Solution explorer panel. |
| [XamlVisualEditor.ToolboxExtension](https://www.nuget.org/packages/XamlVisualEditor.ToolboxExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.ToolboxExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.ToolboxExtension) | Designer toolbox and insertion commands. |
| [XamlVisualEditor.TreeInspectorExtension](https://www.nuget.org/packages/XamlVisualEditor.TreeInspectorExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.TreeInspectorExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.TreeInspectorExtension) | Visual and logical tree inspectors. |
| [XamlVisualEditor.VscodeCompatExtension](https://www.nuget.org/packages/XamlVisualEditor.VscodeCompatExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.VscodeCompatExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.VscodeCompatExtension) | VS Code compatibility runtime host. |
| [XamlVisualEditor.WorkspaceExtension](https://www.nuget.org/packages/XamlVisualEditor.WorkspaceExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.WorkspaceExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.WorkspaceExtension) | Workspace commands and lifecycle integration. |
| [XamlVisualEditor.XamlEditorExtension](https://www.nuget.org/packages/XamlVisualEditor.XamlEditorExtension) | [![NuGet](https://img.shields.io/nuget/v/XamlVisualEditor.XamlEditorExtension.svg)](https://www.nuget.org/packages/XamlVisualEditor.XamlEditorExtension) | XAML editor property metadata contributions. |

## Extension development

Extension packages contain `xve.extension.json` at the package root and a .NET
assembly implementing `IXveExtension`. The host exposes typed services for
commands, views, workspaces, diagnostics, editors, navigation, terminals,
settings, storage, logging, dialogs, permissions, and panel hosts.

See the [extension guide](docs/EXTENSIONS.md), [migration guide](docs/EXTENSION-MIGRATION-GUIDE.md),
and sample projects in `tools/ExtensionSamples`. To pack the hello sample:

```bash
dotnet pack tools/ExtensionSamples/HelloExtension/HelloExtension.csproj -c Release
```

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/XamlVisualEditor.App` | Avalonia application, composition root, views, and resources. |
| `src/XamlVisualEditor.Shell*` | Main window, documents, docking, panels, and shell ViewModels. |
| `src/XamlVisualEditor.Extensions` | Extension SDK contracts and hosting infrastructure. |
| `src/XamlVisualEditor.Xaml.*` | Parsing, AST, serialization, intellisense, and language services. |
| `src/XamlVisualEditor.Designer.*` | Designer abstractions, rendering, adorners, drag-and-drop, and preview host. |
| `extensions/` | Built-in extension projects and generated package manifests. |
| `tests/` | Unit, integration, performance, and Avalonia Headless UI tests. |
| `tools/` | Extension samples, harnesses, CLIs, and host experiments. |
| `site/` | Lunet documentation site deployed to GitHub Pages. |

## Documentation

Build the documentation site locally with:

```bash
./build-docs.sh
cd site
dotnet tool run lunet serve
```

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) and the authoritative engineering rules
in [AGENTS.md](AGENTS.md). Before opening a pull request, build the Release
solution, run the relevant test projects, validate packages, and build the docs.

## License

XamlVisualEditor is licensed under the [MIT License](LICENSE).
