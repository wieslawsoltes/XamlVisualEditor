---
title: Architecture
description: Understand the XamlVisualEditor shell, DI composition root, Dock layout, ViewModels, services, and extension-first feature model.
---

# Architecture

XamlVisualEditor is layered around a passive Avalonia UI, ReactiveUI ViewModels,
domain/services projects, and infrastructure adapters. The app composition root
is `src/XamlVisualEditor.App/App.axaml.cs`.

## Layers

| Layer | Responsibilities |
| --- | --- |
| UI | Avalonia views, control themes, resource dictionaries, and bindings. |
| Presentation | ReactiveUI ViewModels, commands, state composition, interactions, and Dock model orchestration. |
| Domain/Services | XAML parsing, AST, serialization, sync, workspace, language, terminal, debugger, and collaboration rules. |
| Infrastructure | File system, processes, PTY providers, protocol transports, package loading, persistence, and external tools. |

## Shell

The shell owns application lifetime, main window creation, service registration,
Dock layout factories, top-level command orchestration, and built-in extension
activation. Feature panels are contributed through extension contracts whenever
possible.

## Extension-first feature model

Built-in extensions are registered in the app DI container and activated by
`BuiltInExtensionHost`. They register command handlers, metadata, views, menus,
toolbars, property editors, language/debugger services, and panel providers.

## Docking

Dock state is modeled in ViewModels and factories. Views render the Dock model
without owning layout rules. Persisted layout support should remain in the
presentation/service layer rather than in view code.

