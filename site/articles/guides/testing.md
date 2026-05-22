---
title: Testing
description: Run and extend XamlVisualEditor unit, integration, UI headless, and performance tests.
---

# Testing

The repository uses xUnit for unit/integration/performance tests and Avalonia
Headless for UI tests.

## Test projects

| Project | Coverage |
| --- | --- |
| `XamlVisualEditor.Tests.Unit` | Core services, AST, parsing, serialization, sync, language services, Git parsers, terminal, ACP, extension registries, ViewModels, and host adapters. |
| `XamlVisualEditor.Tests.Integration` | ACP sessions, DAP protocol, debug target validation, previewer protocol, LSP diagnostics, extension host spike, workspace loading, and XAML round trips. |
| `XamlVisualEditor.Tests.UI` | Avalonia Headless main window, menu/command integration, extension views, Git panel, ACP panel, terminal control, designer, and navigation flows. |
| `XamlVisualEditor.Tests.Performance` | Large XAML parse/serialize/sync and intellisense thresholds. |

## Commands

```bash
dotnet test tests/XamlVisualEditor.Tests.Unit/XamlVisualEditor.Tests.Unit.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.Integration/XamlVisualEditor.Tests.Integration.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.UI/XamlVisualEditor.Tests.UI.csproj -c Release
dotnet test tests/XamlVisualEditor.Tests.Performance/XamlVisualEditor.Tests.Performance.csproj -c Release
```

## Guidance

- Unit-test ViewModels and services directly.
- Use integration tests for parsing, IO, protocols, previewer, LSP, ACP, and
  docking persistence behavior.
- Use Avalonia Headless for shell, panel, command, input, and rendering workflows.
- Keep performance tests deterministic and focused on measurable hot paths.

