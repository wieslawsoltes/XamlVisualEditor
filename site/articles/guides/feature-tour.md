---
title: Feature Tour
description: Tour the major XamlVisualEditor application surfaces and built-in extensions.
---

# Feature Tour

XamlVisualEditor is organized as an extension-first IDE shell. Built-in
extensions contribute most major panels and commands, while the shell owns window
lifetime, Dock layout orchestration, core document state, and composition.

## Editing and design

- AvaloniaEdit code editor for XAML and text documents.
- Live design surface backed by XAML AST parsing, rendering, and sync.
- Split/code/design/canvas document modes.
- Selection adorners, resize handles, grid/ruler layers, snap lines, margin and
  padding guides, drag/drop, and tree sync.
- Property editor for selected nodes and value editors.

## Workspace and navigation

- Solution Explorer and File Explorer panels.
- Workspace build, rebuild, clean, and startup project commands.
- Quick open, go to line, document symbols, workspace symbols, references,
  history, find in files, and diagnostics navigation.
- Output and Problems panels for tool and language feedback.

## Runtime tooling

- External previewer host for live Avalonia preview workflows.
- DAP and .NET SDK debugging integrations.
- Breakpoints, call stack, locals, watches, debug settings, and execution line
  rendering.
- Integrated terminal with PTY providers, scrollback, keyboard/mouse handling,
  and managed terminal emulation.

## Extension and automation

- Native `IXveExtension` packages with command, view, toolbar, menu, language
  server, debugger, setting, storage, and permission services.
- MCP, ACP, IDE bridge, and VS Code compatibility surfaces.
- Collaboration panel and ProEdit-backed AST mutation bridge.
- Animation editor, toolbox, tree inspector, Git, LSP settings, and template
  wizard extensions.

