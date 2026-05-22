---
title: Project Map
description: Map the major XamlVisualEditor source, extension, test, and tool projects.
---

# Project Map

## Application and shell

| Project | Role |
| --- | --- |
| `XamlVisualEditor.App` | Avalonia app entry point, views, resources, composition root, and host services. |
| `XamlVisualEditor.Shell.ViewModels` | Shell ViewModels, documents, commands, adapters, Dock factories, and panel orchestration. |
| `XamlVisualEditor.Shell` | Dock factory and layout definitions. |

## XAML and designer

| Project | Role |
| --- | --- |
| `XamlVisualEditor.Xaml.Parsing` | XamlX-backed parsing and diagnostics. |
| `XamlVisualEditor.Xaml.Ast` | Mutable AST model, visitors, and change tracking. |
| `XamlVisualEditor.Xaml.Serialization` | AST-to-XAML text serialization. |
| `XamlVisualEditor.Xaml.Intellisense` | Schema inference, completion providers, and XML namespace resolution. |
| `XamlVisualEditor.Designer.Core` | Design item, selection, hit-test, and host abstractions. |
| `XamlVisualEditor.Designer.Rendering` | AST-to-Avalonia control instantiation and visual mapping. |
| `XamlVisualEditor.Designer.Adorners` | Selection, resize, ruler, grid, snap, and spacing overlays. |
| `XamlVisualEditor.Designer.DragDrop` | Toolbox insertion and design/tree reorder protocol. |
| `XamlVisualEditor.Designer.PreviewerHost` | External previewer host process. |

## Services and infrastructure

| Project | Role |
| --- | --- |
| `XamlVisualEditor.Core` | Shared interfaces, models, enums, Git parsers, diagnostics, and domain primitives. |
| `XamlVisualEditor.Extensions` | Extension SDK, manifests, registries, package loading, and host adapters. |
| `XamlVisualEditor.Workspace` | MSBuild workspace, dotnet CLI, template discovery, and assembly resolution. |
| `XamlVisualEditor.Language` | Language service registry and shared helpers. |
| `XamlVisualEditor.CSharp.Language` | Roslyn-backed C# language services. |
| `XamlVisualEditor.Xaml.Language` | XAML completion, diagnostics, and hover services. |
| `XamlVisualEditor.Lsp` | JSON-RPC/LSP client, transport, routing, diagnostics, and test hooks. |
| `XamlVisualEditor.Sync` | Text, AST, designer, and collaboration synchronization. |
| `XamlVisualEditor.Terminal` | Terminal emulator, PTY providers, parser, buffer, and capture/replay. |
| `XamlVisualEditor.Acp` | Agent Client Protocol client, host, permissions, profiles, settings, and secrets. |

## Extensions

The `extensions/` folder contains built-in extensions for ACP, MCP, IDE bridge,
VS Code compatibility, File Explorer, Solution Explorer, Workspace commands,
Toolbox, Property Editor, Output/Problems, Navigation, XAML editor metadata,
Tree Inspector, Animation Editor, Collaboration, Debug Settings, LSP Settings,
Git, .NET templates, DAP debugging, and .NET SDK debugging.

