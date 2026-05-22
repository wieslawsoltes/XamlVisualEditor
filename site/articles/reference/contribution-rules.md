---
title: Contribution Rules
description: Engineering rules and validation expectations for XamlVisualEditor contributors.
---

# Contribution Rules

Contributor work should follow the project engineering guide in `AGENTS.md`.

## Architectural rules

- Apply SOLID strictly.
- Keep views passive and use XAML for layout and visuals.
- Route input through bindings, commands, and behaviors.
- Keep ViewModels ReactiveUI-based and UI-framework agnostic where practical.
- Depend on abstractions and wire concrete services in the composition root.
- Use Dock model state for docking layout.
- Use AvaloniaEdit for code/text editing surfaces.
- Use ProDataGrid for tabular, tree, and list data presentation.
- Avoid reflection unless explicitly approved.

## Validation rules

- Add unit tests for production logic.
- Use Avalonia Headless for UI flows.
- Use integration tests for parsing, IO, protocols, previewer, LSP, ACP, and
  docking persistence behavior.
- Keep performance changes measurable and documented.
- Build the solution and the Lunet site before publishing changes.

