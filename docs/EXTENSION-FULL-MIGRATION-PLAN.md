# Extension Full-Migration Plan

## Scope

Complete migration of remaining IDE interactions to the extension command/API model, with shell code limited to lifecycle/layout orchestration and host implementations behind extension contracts.

## Findings from code audit

1. **Startup project selection path was still shell-local**
   - `SolutionExplorerViewModel.SetStartupProjectCommand` invoked shell event wiring directly.
   - No extension command/API contract existed for setting startup project from extension flows.
2. **Non-core command handlers still exist in shell implementation**
   - Command handlers still execute in shell, but are now invoked through typed `IWorkspaceCommands` APIs.
3. **Some panel host adapters still exposed raw host view models**
   - These were functional, but not final typed extension-facing APIs.

## Migration plan

### Phase 1 — Close startup project command gap (implemented)

- Add workspace startup selection contract to extension API:
  - `IWorkspaceCommands.SetStartupProjectAsync(string projectPath, string? targetFramework, CancellationToken ct)`
  - `IWorkspaceModel.SetStartupProjectAsync(string projectPath, string? targetFramework, CancellationToken ct)`
- Add extension command:
  - `workspace.setStartupProject` in `WorkspaceExtension`
- Route Solution Explorer context command through extension command execution.
- Remove shell-only event wiring for startup project selection.

### Phase 2 — Replace shell bridge command paths (implemented)

- Added typed APIs for editing/debug/run/terminal operations on `IWorkspaceCommands`.
- Workspace extension command handlers now invoke typed contracts directly.
- Removed `IShellCommandBridge`/`ShellCommandBridgeAdapter` path from app composition.

### Phase 3 — Remove raw view-model panel host contracts (implemented)

- Replaced raw `ViewModel` host contracts with typed panel model interfaces:
  - `IAnimationEditorPanelModel`
  - `ICollaborationPanelModel`
  - `ISolutionExplorerPanelModel`
- Updated shell adapters and host-owned panel models to implement the typed contracts.
- Kept extension projects independent from `XamlVisualEditor.Shell.ViewModels`.

## Acceptance criteria

- Startup project can be set from Solution Explorer via extension command path.
- Startup project API is available in extension contracts.
- Host-owned panel adapters expose typed panel models instead of raw `object` view models.
- Extension migration docs reflect updated state.
- Unit tests cover new startup project command contract path.
