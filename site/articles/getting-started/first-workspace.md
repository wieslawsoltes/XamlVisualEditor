---
title: First Workspace
description: Open a project, use the editor and designer, and run workspace commands in XamlVisualEditor.
---

# First Workspace

1. Start the app with `dotnet run --project src/XamlVisualEditor.App/XamlVisualEditor.App.csproj`.
2. Open a solution, project, folder, or XAML file from the File menu.
3. Use Solution Explorer or File Explorer to navigate the workspace.
4. Open a XAML document and switch between code, design, split, and canvas surfaces.
5. Use the command palette for workspace, navigation, debug, terminal, and extension commands.

## Typical workflow

- Edit XAML in the AvaloniaEdit surface.
- Inspect the live designer and tree panels.
- Select elements and update values in the property editor.
- Use output/problems panels to inspect build, diagnostic, and tool feedback.
- Run workspace commands such as build, rebuild, clean, startup project selection,
  quick open, go to line, symbols, and find in files.
- Start the previewer or debugger when the workspace is trusted and configured.

## Workspace services

Workspace state is surfaced through typed services and extension contracts:

- `IWorkspace` for file discovery, reads, writes, watchers, and configuration.
- `IWorkspaceCommands` for load, restore, build, rebuild, clean, startup project,
  editor, navigation, debug, run, and terminal operations.
- `IWorkspaceInfo` for workspace path and project state shared across extensions.

