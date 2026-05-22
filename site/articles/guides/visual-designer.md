---
title: Visual Designer
description: Work with the XamlVisualEditor live designer, design surface, tree panels, adorners, and property editor.
---

# Visual Designer

The designer turns XAML AST nodes into a live Avalonia control tree, maps visuals
back to source nodes, and keeps the editing surface synchronized with document
state.

## Designer surfaces

- `XamlVisualEditor.Designer.Core` defines design items, selections, hit testing,
  and designer host abstractions.
- `XamlVisualEditor.Designer.Rendering` instantiates live Avalonia controls from
  AST nodes and maintains visual-to-node mapping.
- `XamlVisualEditor.Designer.Adorners` renders selection rectangles, resize
  handles, grids, rulers, snap lines, and margin/padding guides.
- `XamlVisualEditor.Designer.DragDrop` handles toolbox insertion, surface
  rearrangement, and tree reorder workflows.

## Supporting panels

- Toolbox contributes insert commands.
- Property Editor shows categorized or searchable selected-node properties.
- Tree Inspector exposes visual and logical tree views.
- Animation Editor handles timeline/keyframe editing.

## Implementation notes

Designer logic belongs in services and ViewModels. Views render bound state and
route input through Avalonia bindings and behaviors. Keep shell-specific wiring in
host adapters and keep reusable designer rules in domain/services projects.

