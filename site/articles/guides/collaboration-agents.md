---
title: Collaboration and Agents
description: Use XamlVisualEditor collaboration, ACP, MCP, IDE bridge, and automation extension surfaces.
---

# Collaboration and Agents

XamlVisualEditor includes collaboration and automation infrastructure for local
and external tools.

## Collaboration

- `XamlVisualEditor.Collaboration` bridges XAML AST mutations to CRDT operations
  through the ProEdit collaboration stack.
- `XamlVisualEditor.Collaboration.UI` provides participant, presence, and session
  ViewModels.
- The collaboration extension contributes the user-facing panel and commands.

## Agent Client Protocol

ACP support includes:

- JSON-RPC client and stdio transport.
- Agent host process management.
- OAuth/device-flow helper services.
- Profile and settings stores.
- Secret storage and permission models.
- Tool panel integration through the ACP extension.

## Automation bridges

- MCP extension for model context protocol workflows.
- IDE Bridge extension and CLI tooling for host integration.
- VS Code compatibility host for selected extension-host style interactions.

