---
title: Workspace and Language Services
description: Understand workspace loading, file services, Roslyn C# services, XAML services, and LSP routing.
---

# Workspace and Language Services

XamlVisualEditor combines internal language services with external LSP routing
and MSBuild workspace services.

## Workspace services

- `WorkspaceService` loads MSBuild projects and resolves assemblies.
- `DotNetCliRunner` executes .NET CLI commands.
- `DotNetTemplateService` discovers and applies .NET templates.
- `FileSystemWorkspace` exposes workspace files to extensions.
- `WorkspaceInfoService` shares active workspace state.

## Language services

- `XamlLanguageService` provides XAML completion, diagnostics, and hover support.
- `CSharpLanguageService` uses Roslyn workspaces for C# diagnostics, completion,
  and navigation.
- `LanguageServiceRegistry` composes available providers.
- `LspLanguageServiceRouter` routes requests to configured external language
  servers.

## Panels and commands

Workspace and language features surface through built-in extensions:

- Workspace commands for load, restore, build, rebuild, clean, startup project,
  editing, debug/run, and terminal actions.
- Navigation extension for quick open, find in files, go to line, references,
  symbols, and history.
- LSP settings extension for server configuration.
- Solution Explorer, File Explorer, Output, and Problems panels.

