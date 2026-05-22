---
title: Home
layout: simple
description: XamlVisualEditor is an extensible Avalonia XAML visual editor with a live designer, code editor, language services, debugging, terminal, collaboration, and native .NET extensions.
og_type: website
---

# XamlVisualEditor

XamlVisualEditor is a native Avalonia XAML design and development environment
with a VS Code-inspired extension model. It brings together a live design
surface, AvaloniaEdit code editing, XAML AST synchronization, language services,
debugger tooling, integrated terminal support, Dock-based panels, and automation
bridges for agents and external tools.

## What it includes

- Live XAML parse, AST, serialization, and sync services.
- Avalonia visual designer with selection, drag/drop, adorners, rulers, guides,
  and previewer integration.
- AvaloniaEdit code editor with syntax highlighting, diagnostics, completion,
  semantic tokens, breakpoints, and execution markers.
- Dock-based shell with persisted tool windows and extension-contributed menus,
  toolbar actions, command palette entries, status bar items, and views.
- Workspace, solution explorer, file explorer, templates, Git, output/problems,
  references, tree inspection, animation editing, collaboration, ACP, MCP, IDE
  bridge, LSP settings, debugging, and terminal panels.
- Unit, integration, UI headless, and performance test coverage.

## Start here

- [Getting Started](articles/getting-started/readme.md): build, run, and open a
  workspace from source.
- [Feature Tour](articles/guides/feature-tour.md): learn the major editing,
  design, debugging, and automation surfaces.
- [Architecture](articles/concepts/architecture.md): understand shell, DI, AST
  sync, Dock layout, language services, and extension hosting.
- [Extension SDK](articles/guides/extension-sdk.md): package native .NET
  extensions and contribute commands, views, language servers, and tools.
- [CI and Release](articles/reference/ci-release.md): use the repository
  workflows and Lunet documentation pipeline.

## Source layout

| Area | Description |
| --- | --- |
| `src/XamlVisualEditor.App` | Avalonia app entry point, composition root, views, resources, and top-level host services. |
| `src/XamlVisualEditor.Shell.ViewModels` | Shell ViewModels, Dock factories, documents, panels, and command orchestration. |
| `src/XamlVisualEditor.Extensions` | Extension SDK contracts, package loading, contribution registries, and host adapters. |
| `src/XamlVisualEditor.Xaml.*` | XAML AST, parser, serialization, intellisense, language service, and language server projects. |
| `src/XamlVisualEditor.Designer.*` | Designer abstractions, rendering, drag/drop, adorners, and previewer host. |
| `extensions/` | Built-in extension projects that own major feature panels and commands. |
| `tests/` | xUnit, Avalonia Headless, integration, and performance test projects. |
| `site/` | This Lunet documentation site. |

