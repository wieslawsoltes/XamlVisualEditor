# Extension Migration Closure Plan

## Audit map (2026-02-28) — Implemented

### Gap 1 — Generic shell bridge command path

- Implemented:
  - `WorkspaceExtension` command registrations now call typed `IWorkspaceCommands` APIs.
  - `IWorkspaceCommands` now exposes typed editing/debug/run/terminal methods.

### Gap 2 — Obsolete bridge plumbing in shell

- Implemented:
  - Removed `IShellCommandBridge` and `ShellCommandBridgeAdapter` from app composition.
  - Removed `ExtensionShellCommandBridge.cs` bridge contract.
  - `MainWindowViewModel` now executes typed command methods through direct reactive-command helpers.

### Gap 3 — Documentation drift

- Implemented:
  - Updated `EXTENSION-API.md`, `EXTENSIONS.md`, `SHELL-COMMAND-AUDIT.md`, and `EXTENSION-FULL-MIGRATION-PLAN.md`.

## Validation checklist

- [x] No extension command path depends on `IShellCommandBridge`.
- [x] Workspace extension command handlers execute via typed extension contracts.
- [x] Shell bridge types are removed from source and app DI.
- [x] Targeted unit tests and app build executed after migration.
