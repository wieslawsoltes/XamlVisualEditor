---
title: Service Boundaries
description: Keep XamlVisualEditor UI, presentation, domain, and infrastructure boundaries clear.
---

# Service Boundaries

The project follows strict layering and dependency inversion. Views bind to
ViewModels; ViewModels depend on abstractions; concrete services are wired in the
app composition root.

## Rules of thumb

- Keep views passive and route input through bindings, commands, and behaviors.
- Put command state and orchestration in ViewModels.
- Put XAML parsing, serialization, AST, language, workspace, terminal, debugger,
  collaboration, and protocol logic in services.
- Put file system, processes, PTYs, package loading, and persistence behind
  infrastructure abstractions.
- Register concrete services in `App.axaml.cs`.
- Prefer focused interfaces over broad host-facing service objects.

## Extension boundaries

Extensions should depend on `XamlVisualEditor.Extensions` contracts. Avoid
referencing shell ViewModels from extension code; add typed host adapters when a
feature needs shell-owned state.

