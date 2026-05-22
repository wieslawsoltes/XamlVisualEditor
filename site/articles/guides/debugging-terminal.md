---
title: Debugging and Terminal
description: Use DAP debugging, .NET SDK debugger tooling, panels, and integrated terminal support.
---

# Debugging and Terminal

The runtime tooling stack is extension-based and uses typed debugger and terminal
services.

## Debugging

- `XamlVisualEditor.Debugging.DapExtension` provides DAP-backed .NET debugging.
- `XamlVisualEditor.Debugging.DotNetSdkExtension` integrates .NET SDK debugger
  tooling and adapter discovery.
- Debug settings capture adapter paths, auto-download preferences, and runtime
  status.
- Shell panels expose breakpoints, call stack, locals, watches, and execution
  state.

## Terminal

- `XamlVisualEditor.Terminal` implements a managed terminal emulator, buffer,
  parser, key mapper, capture/replay, and PTY provider abstraction.
- `XamlVisualEditor.Terminal.Avalonia` renders terminal state and routes input in
  Avalonia.
- Platform PTY providers are selected through `PtyProviderFactory`.

## Validation

Debug and terminal behavior is covered by unit tests, integration tests, and
Avalonia Headless UI tests. Terminal golden render tests validate frame output,
while protocol tests validate debugger and previewer message flows.

