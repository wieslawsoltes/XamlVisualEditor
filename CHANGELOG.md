# Changelog

All notable changes to XamlVisualEditor are documented in this file. The project
uses Semantic Versioning.

## [0.2.0] - 2026-09-01

### Added

- Added design-surface theming from the workspace application's `App.axaml`; `XVE_DESIGN_APP_XAML` names the application for library workspaces without one.
- Added the `IDesignPreviewHook` extension point: extensions can take over control instantiation for the design preview or post-process finished nodes.
- Added activation of enabled installed extension packages: the archive is extracted, the manifest's main assembly is loaded, and its extension entry types are activated.
- Added `--mcp` to start the local HTTP MCP server from the command line.
- Added MCP command-line options for the transport, HTTP port, and HTTP path.
- Added per-user persistence for the main-window size and designer/code splitter ratios.
- Added automatic preview-host selection for library workspaces that use a separate application assembly.
- Added preview frame export through the IDE bridge.
- Added visible progress status for workspace loading and design-surface rebuilds.

### Changed

- The design preview now materializes property elements that carry control content and applies attributes outside its built-in list through registered Avalonia or writable CLR properties.
- The design artboard now sizes to the document: an explicit size wins, otherwise the artboard follows the rendered document bounds above the declared minimum instead of clipping at the default size.
- The C# language workspace now warms in the background after the initial workspace load.
- XVE now reads assemblies from a shared build output instead of unrelated output trees.
- Generated AXAML can select a matching source project through `XVE_PROJECT_SOURCE_ROOT`.
- The dock layout now saves before view teardown and loads during the next start.
- The bottom tool dock starts closed and opens only after a user action.
- The designer now applies explicit `Grid.RowDefinitions` and `Grid.ColumnDefinitions` elements.

### Fixed

- Fixed the test suites reading and overwriting the user's persisted layout files: the dock factory settings root is now overridable, and every test bootstrap points it at an isolated temporary directory.
- Fixed the collapsed bottom dock expanding on every start: loading treated the persisted zero proportion as damage and reset it, and extension views registering after startup added and activated their tool over the restored layout. Zero now stays collapsed, and late extension tools go to the pin strip instead of activating.
- Fixed closed tool panels reappearing on every start: the shell recreates missing default and extension tools, so closed tools are now remembered in a persisted list, removed from loaded layouts, skipped by the recreation paths, and revived only through the view menu (a layout reset clears the list).
- Fixed designer selection picking a container instead of the clicked child: hit-testing now walks the real visual tree in z-order (templated controls included) and maps the hit to the nearest design item, instead of probing the logical structure only.
- Fixed a crash when nudging a selected control with arrow keys inside a grid without explicit row or column definitions: the target index underflowed to -1 and Grid.SetRow/SetColumn rejected it, terminating the application.
- Fixed the design surface stealing arrow keys from the code editor: its window-level key handler now acts only while focus is inside the surface.
- Fixed a UI freeze of 30+ seconds when selecting a control with a large property set: the property panel now replaces its entry collections with a single reset instead of per-item inserts, creates entry commands lazily, and notifies entry changes through direct callbacks instead of per-entry subscriptions.
- Fixed the design artboard clipping the document: it froze an early measurement taken before templates and deferred content finished loading, and border strokes on the document edge ended on the clip boundary. The document now sits at the artboard origin and sizes itself, and the artboard follows the rendered bounds plus a small padding.
- Fixed the saved dock layout never loading back: it was serialized polymorphically but deserialized against the concrete root type, and persisted open documents and the pinned dock made the file unreadable, so every start silently deleted it and rebuilt the default layout.
- Fixed a restored layout rendering as empty: the serializer materializes Active-/Default-/FocusedDockable references as duplicate subtrees, so the shell rendered an unwired copy while panels were added to the visible tree; loading now repoints these references into the visible tree by id.
- Fixed pinned tools multiplying across restarts: the layout search helpers now cover the pinned and hidden dockable lists, and loading prunes duplicates that earlier versions accumulated.
- Fixed the main window forgetting its screen position and maximized state; both are now persisted and restored, and a stored position on a disconnected monitor falls back to the default placement.
- Fixed attribute-form `RowDefinitions`/`ColumnDefinitions` being cleared instead of parsed, which collapsed every grid child into one implicit cell.
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
