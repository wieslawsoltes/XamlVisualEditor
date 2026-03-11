# IDE Architecture and Feature Alignment Plan (VS Code / Visual Studio)

## Current architecture snapshot

- Shell is extension-first (`IXveExtension`, command metadata, menu/toolbar/command-palette contributions).
- Main UI is Avalonia + Dock + ReactiveUI (`MainWindowViewModel`, extension contribution registries).
- Navigation capabilities already include definition/references/history, but lacked common editor accelerators.
- Workspace extension API (`IWorkspace`) existed, but app DI still used an in-memory workspace backend.

## Gap map vs common VS Code / Visual Studio workflow

1. **Quick Open parity gap (Ctrl+P)**  
   No extension command for file quick-open across workspace files.
2. **Go To Line parity gap (Ctrl+G)**  
   No direct line/column navigation command.
3. **Workspace API runtime gap**  
   `IWorkspace` in app runtime was in-memory, limiting `find/read/write/watch` usefulness for extensions.
4. **Status bar extension gap**  
   `IStatusBarItem` creation worked in API but host rendering was not wired into the main status bar.
5. **Find in Files parity gap (Ctrl+Shift+F)**  
   No workspace-wide text search command that routes matches to a navigable results view.
6. **Navigation symbol keybinding parity gap**  
   Document/workspace symbol commands existed without common VS Code-style default accelerators.
7. **Problems navigation parity gap (Ctrl+Shift+M / F8 / Shift+F8)**  
   Problems panel existed, but lacked standard shortcut-driven show/next/previous navigation workflow.

## Implementation plan

1. Add a filesystem-backed `IWorkspace` implementation in app services.
2. Switch app DI to use filesystem workspace backend.
3. Add navigation extension commands:
   - `navigation.quickOpen` (Ctrl+P)
   - `navigation.goToLine` (Ctrl+G)
4. Contribute those commands to Edit menu and command palette.
5. Remove keybinding conflict by moving previewer shortcut away from Ctrl+P.
6. Add targeted unit tests and run build/tests.
7. Wire extension-created status bar items into shell status bar rendering.
8. Add `navigation.findInFiles` (Ctrl+Shift+F) to search workspace text and publish matches in references panel.
9. Add symbol navigation keybindings:
   - `navigation.documentSymbols` (`Ctrl+Shift+O`)
   - `navigation.workspaceSymbols` (`Ctrl+T`)
10. Add problems navigation commands in output extension:
   - `problems.show` (`Ctrl+Shift+M`)
   - `problems.next` (`F8`)
   - `problems.previous` (`Shift+F8`)
11. Add targeted tests for diagnostics navigation behavior.

## Implemented

- [x] Added `FileSystemWorkspace` (`src/XamlVisualEditor.App/Services/FileSystemWorkspace.cs`) with:
  - glob-based file discovery
  - file read/write against active workspace root
  - file-system watcher bridge for extension consumers
- [x] Switched DI from `InMemoryWorkspace` to `FileSystemWorkspace` (`src/XamlVisualEditor.App/App.axaml.cs`).
- [x] Added `navigation.quickOpen` and `navigation.goToLine` in `extensions/XamlVisualEditor.NavigationExtension/NavigationExtension.cs`.
- [x] Added command metadata, menu items, and command palette entries for both commands.
- [x] Updated previewer shortcut text from `Ctrl+P` to `Ctrl+Alt+P` to preserve VS Code-style quick open binding in the shell (`src/XamlVisualEditor.App/MainWindow.axaml`).
- [x] Added unit tests:
  - `tests/XamlVisualEditor.Tests.Unit/App/FileSystemWorkspaceTests.cs`
  - `tests/XamlVisualEditor.Tests.Unit/Extensions/NavigationExtensionTests.cs`
- [x] Implemented status bar host integration:
  - `AppWindow.CreateStatusBarItem` now produces live items synchronized into shell state.
  - `MainWindowViewModel` now tracks left/right extension status bar items and command bindings.
  - `MainWindow.axaml` now renders extension status bar items on both sides of the status strip.
  - startup now syncs pre-window-created status bar items after `MainWindow` is attached.
  - test coverage added in `tests/XamlVisualEditor.Tests.Unit/StatusBarIntegrationTests.cs`.
- [x] Added `navigation.findInFiles` (`Ctrl+Shift+F`) in `extensions/XamlVisualEditor.NavigationExtension/NavigationExtension.cs`:
  - prompts for query (pre-filled from active selection when available)
  - scans workspace text files with guardrails (excluded folders, file-size cap, match cap)
  - publishes navigable matches to the references panel and reveals it
- [x] Added/validated navigation symbol keybindings in `extensions/XamlVisualEditor.WorkspaceExtension/WorkspaceExtension.cs`:
  - document symbols: `Ctrl+Shift+O`
  - workspace symbols: `Ctrl+T`
- [x] Extended unit coverage:
  - `tests/XamlVisualEditor.Tests.Unit/Extensions/NavigationExtensionTests.cs`
  - `tests/XamlVisualEditor.Tests.Unit/Extensions/WorkspaceExtensionTests.cs`
- [x] Added problems navigation parity in `extensions/XamlVisualEditor.OutputExtension/OutputExtension.cs`:
  - `problems.show` (`Ctrl+Shift+M`) to surface/focus Problems panel
  - `problems.next` (`F8`) and `problems.previous` (`Shift+F8`) for keyboard-driven diagnostics navigation
  - command palette entries for new problems commands
- [x] Extended `ProblemsPanelViewModel` with relative diagnostic navigation and open-location flow (`NavigateToRelativeAsync`).
- [x] Added unit tests:
  - `tests/XamlVisualEditor.Tests.Unit/Extensions/OutputExtensionTests.cs`

## Validation checklist

- [x] Changed projects build successfully.
- [x] New targeted unit tests pass.
- [x] Full solution build/tests pass.
