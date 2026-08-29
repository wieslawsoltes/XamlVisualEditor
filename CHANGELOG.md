# Changelog

All notable changes to XamlVisualEditor are documented in this file. The project
uses Semantic Versioning.

## [Unreleased]

### Added

- Added `--mcp` to start the local HTTP MCP server from the command line.
- Added MCP command-line options for the transport, HTTP port, and HTTP path.
- Added per-user persistence for the main-window size and designer/code splitter ratios.
- Added automatic preview-host selection for library workspaces that use a separate application assembly.
- Added preview frame export through the IDE bridge.
- Added visible progress status for workspace loading and design-surface rebuilds.

### Changed

- The C# language workspace now warms in the background after the initial workspace load.
- XVE now reads assemblies from a shared build output instead of unrelated output trees.
- Generated AXAML can select a matching source project through `XVE_PROJECT_SOURCE_ROOT`.
- The dock layout now saves before view teardown and loads during the next start.
- The bottom tool dock starts closed and opens only after a user action.
- The designer now applies explicit `Grid.RowDefinitions` and `Grid.ColumnDefinitions` elements.

### Fixed

- Fixed indefinite project loading when MSBuild stalls or fails during metadata extraction.
- Fixed workspace access errors caused by changes to the process-wide current directory.
- Fixed preview startup for library projects that do not contain an application entry point.
- Fixed preview sessions that displayed an endless wait after the preview host stopped.
- Fixed missing preview-host errors in the preview surface.
- Fixed concurrent access to `recent-files.json` during application startup.
- Fixed incorrect workspace selection for generated AXAML outside a source project.

## [0.1.0] - 2026-08-20

### Added

- Cross-platform Avalonia XAML visual editor with live designer and code editor.
- XAML parsing, AST, serialization, intellisense, C# services, and LSP support.
- Dock-based shell, workspace tooling, terminal, Git, debugging, collaboration,
  ACP, MCP, animation, property editing, and tree inspection.
- Native .NET extension SDK and built-in extension package set.
- Unit, integration, performance, and Avalonia Headless UI test suites.
- Linux, Windows, and macOS release archives for x64 and Arm64.
- Source-linked NuGet packages and symbol packages.

[Unreleased]: https://github.com/wieslawsoltes/XamlVisualEditor/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/wieslawsoltes/XamlVisualEditor/releases/tag/v0.1.0
