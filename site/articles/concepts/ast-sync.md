---
title: XAML AST Sync
description: How XamlVisualEditor keeps XAML text, AST nodes, designer visuals, and collaboration operations synchronized.
---

# XAML AST Sync

The editing model is centered on a mutable XAML AST. Text changes, designer
changes, tree edits, property edits, and collaboration operations all flow
through typed services so the document can stay coherent.

## Pipeline

1. XAML text is parsed by `XamlParsingService`.
2. AST nodes are stored in `XamlVisualEditor.Xaml.Ast` models with change
   tracking.
3. The designer renders nodes into Avalonia controls and maps visuals back to
   AST node identities.
4. Property, tree, drag/drop, and designer operations mutate AST state.
5. `XamlSerializationService` writes AST changes back to XAML text while
   preserving whitespace where possible.
6. `SyncEngine` coordinates document, designer, and collaboration state.

## Design goals

- Preserve user-authored XAML as much as possible.
- Keep source and designer updates reversible and testable.
- Avoid reflection-driven editor behavior where typed models or generated code
  can represent the same contract.
- Keep business rules outside Avalonia view code.

