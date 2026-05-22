---
title: XAML Editor
description: Use the AvaloniaEdit-based XAML editor, diagnostics, completion, semantic tokens, and AST synchronization.
---

# XAML Editor

The code editor is built around AvaloniaEdit and the XAML language services in
the `src/XamlVisualEditor.Xaml.*` projects.

## Editing pipeline

- `XamlVisualEditor.Xaml.Parsing` parses XAML through XamlX and reports
  diagnostics.
- `XamlVisualEditor.Xaml.Ast` stores mutable nodes with change tracking and
  visitor support.
- `XamlVisualEditor.Xaml.Serialization` writes AST changes back to XAML text with
  whitespace preservation and minimal edits.
- `XamlVisualEditor.Sync` coordinates bidirectional updates between text, AST,
  designer, and collaboration surfaces.

## Editor features

- TextMate/AvaloniaEdit syntax highlighting.
- Completion providers and schema inference.
- Semantic token colorization.
- Diagnostics and error markers.
- Breakpoint margin and execution-line rendering.
- Rename, format, code actions, symbols, and language navigation commands.

## When to add editor behavior

Prefer adding behavior through ViewModels, editor service abstractions, or
extension commands. Keep UI-specific rendering in the editor view layer and keep
language rules inside language service projects.

