---
title: Extension Hosting
description: How XamlVisualEditor loads, activates, and hosts built-in and packaged extensions.
---

# Extension Hosting

Extensions implement `IXveExtension` and receive an `ExtensionContext` during
activation. The context provides typed services for commands, views, contributions,
workspace access, dialogs, language services, diagnostics, terminal access,
storage, logging, permissions, and panel hosts.

## Built-in extensions

Built-in extensions are registered through DI and activated at startup. They own
most feature panels, including File Explorer, Solution Explorer, Toolbox,
Property Editor, Output/Problems, Navigation, Tree Inspector, Animation Editor,
Collaboration, Debug Settings, LSP Settings, ACP, MCP, IDE Bridge, Git, and
debugger integrations.

## Packaged extensions

Packaged extensions are NuGet packages with an `xve.extension.json` manifest at
the package root. The extension manager installs packages, persists enabled
state, and uses the package loader to parse manifests and locate assemblies.

## Compatibility hosts

The repository also contains IDE Bridge and VS Code compatibility experiments.
Use these as integration surfaces, not as replacements for native typed
extension APIs.

