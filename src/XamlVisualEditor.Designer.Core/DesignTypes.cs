using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Designer.Core;

/// <summary>
/// Concrete implementation of <see cref="IDesignItem"/> that bridges
/// an AST node to its instantiated Avalonia control.
/// </summary>
public sealed class DesignItem : ReactiveObject, IDesignItem
{
    private readonly MutableAstObjectNode _astNode;
    private readonly List<DesignItem> _children = new();
    private readonly List<IPropertyDescriptor> _properties = new();

    /// <summary>
    /// Creates a new design item for the specified AST node.
    /// </summary>
    public DesignItem(MutableAstObjectNode astNode, Control? visualElement = null)
    {
        _astNode = astNode;
        VisualElement = visualElement;
    }

    /// <inheritdoc />
    public Guid AstNodeId => _astNode.Id;

    /// <summary>
    /// Gets the underlying AST node.
    /// </summary>
    public MutableAstObjectNode AstNode => _astNode;

    /// <inheritdoc />
    public string TypeName => _astNode.TypeName;

    /// <summary>
    /// Gets or sets the instantiated Avalonia control.
    /// </summary>
    [Reactive]
    public Control? VisualElement { get; set; }

    /// <inheritdoc />
    public IDesignItem? Parent { get; internal set; }

    /// <inheritdoc />
    public IReadOnlyList<IDesignItem> Children => _children;

    /// <inheritdoc />
    public IReadOnlyList<IPropertyDescriptor> Properties => _properties;

    /// <summary>
    /// Gets the bounds of the visual element relative to its parent.
    /// For surface-relative bounds, use <see cref="GetBoundsRelativeTo"/>.
    /// </summary>
    public Rect Bounds => VisualElement?.Bounds ?? default;

    /// <summary>
    /// Gets the bounds of the visual element translated to the coordinate space
    /// of the specified ancestor control (typically the design surface root).
    /// Falls back to <see cref="Bounds"/> if translation is not possible.
    /// </summary>
    public Rect GetBoundsRelativeTo(Control? ancestor)
    {
        if (VisualElement is null || ancestor is null)
        {
            return Bounds;
        }

        Point? translated = VisualElement.TranslatePoint(new Point(0, 0), ancestor);
        if (translated is null)
        {
            return Bounds;
        }

        return new Rect(translated.Value, VisualElement.Bounds.Size);
    }

    /// <summary>
    /// Gets whether this item is currently selected.
    /// </summary>
    [Reactive]
    public bool IsSelected { get; set; }

    /// <inheritdoc />
    public void SetProperty(string name, string? value)
    {
        _astNode.SetPropertyValue(name, value);
    }

    /// <inheritdoc />
    public string? GetProperty(string name)
    {
        return _astNode.GetPropertyValue(name);
    }

    /// <summary>
    /// Adds a child design item.
    /// </summary>
    public void AddChild(DesignItem child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>
    /// Removes a child design item.
    /// </summary>
    public bool RemoveChild(DesignItem child)
    {
        child.Parent = null;
        return _children.Remove(child);
    }

    /// <summary>
    /// Sets the property descriptors for this item.
    /// </summary>
    public void SetProperties(IEnumerable<IPropertyDescriptor> descriptors)
    {
        _properties.Clear();
        _properties.AddRange(descriptors);
    }
}

/// <summary>
/// Manages the selection state on the design surface.
/// </summary>
public sealed class SelectionManager : ReactiveObject
{
    private readonly List<IDesignItem> _selectedItems = new();

    /// <summary>
    /// Gets the currently selected items.
    /// </summary>
    public IReadOnlyList<IDesignItem> SelectedItems => _selectedItems;

    /// <summary>
    /// Gets the primary (first) selected item.
    /// </summary>
    [Reactive]
    public IDesignItem? PrimarySelection { get; private set; }

    /// <summary>
    /// Fires when the selection changes.
    /// </summary>
    public event Action<IReadOnlyList<IDesignItem>>? SelectionChanged;

    /// <summary>
    /// Selects a single item, optionally adding to the selection.
    /// </summary>
    public void Select(IDesignItem item, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            ClearSelectionInternal();
        }

        if (!_selectedItems.Contains(item))
        {
            _selectedItems.Add(item);
            if (item is DesignItem di)
            {
                di.IsSelected = true;
            }
        }

        PrimarySelection = _selectedItems.Count > 0 ? _selectedItems[0] : null;
        SelectionChanged?.Invoke(_selectedItems);
    }

    /// <summary>
    /// Clears the selection.
    /// </summary>
    public void ClearSelection()
    {
        ClearSelectionInternal();
        PrimarySelection = null;
        SelectionChanged?.Invoke(_selectedItems);
    }

    /// <summary>
    /// Toggles selection of an item.
    /// </summary>
    public void ToggleSelection(IDesignItem item)
    {
        if (_selectedItems.Contains(item))
        {
            _selectedItems.Remove(item);
            if (item is DesignItem di)
            {
                di.IsSelected = false;
            }
        }
        else
        {
            _selectedItems.Add(item);
            if (item is DesignItem di)
            {
                di.IsSelected = true;
            }
        }

        PrimarySelection = _selectedItems.Count > 0 ? _selectedItems[0] : null;
        SelectionChanged?.Invoke(_selectedItems);
    }

    private void ClearSelectionInternal()
    {
        foreach (IDesignItem item in _selectedItems)
        {
            if (item is DesignItem di)
            {
                di.IsSelected = false;
            }
        }
        _selectedItems.Clear();
    }
}

/// <summary>
/// ViewModel for the design surface.
/// </summary>
public sealed class DesignSurfaceViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the selection manager.
    /// </summary>
    public SelectionManager Selection { get; } = new();

    /// <summary>
    /// Gets or sets the root design item.
    /// </summary>
    [Reactive]
    public DesignItem? RootItem { get; set; }

    private IReadOnlyDictionary<Guid, DesignItem> _itemMap = new Dictionary<Guid, DesignItem>();
    private IReadOnlyDictionary<Control, DesignItem> _controlMap = new Dictionary<Control, DesignItem>();

    /// <summary>
    /// Gets the design items indexed by AST node id.
    /// </summary>
    public IReadOnlyDictionary<Guid, DesignItem> ItemMap => _itemMap;

    /// <summary>
    /// Gets the design items indexed by control instance.
    /// </summary>
    public IReadOnlyDictionary<Control, DesignItem> ControlMap => _controlMap;

    /// <summary>
    /// Gets or sets the zoom level (1.0 = 100%).
    /// </summary>
    [Reactive]
    public double Zoom { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets whether snap lines are enabled.
    /// </summary>
    [Reactive]
    public bool SnapLinesEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the surface is in edit mode (non-interactive preview).
    /// </summary>
    [Reactive]
    public bool IsEditMode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the grid is visible.
    /// </summary>
    [Reactive]
    public bool ShowGrid { get; set; }

    /// <summary>
    /// Gets or sets whether snap to grid is enabled.
    /// </summary>
    [Reactive]
    public bool SnapToGrid { get; set; } = true;

    /// <summary>
    /// Gets or sets the grid snap size.
    /// </summary>
    [Reactive]
    public double GridSnapSize { get; set; } = 8.0;

    /// <summary>
    /// Gets or sets the canvas width.
    /// </summary>
    [Reactive]
    public double CanvasWidth { get; set; } = 800;

    /// <summary>
    /// Gets or sets the canvas height.
    /// </summary>
    [Reactive]
    public double CanvasHeight { get; set; } = 600;

    /// <summary>
    /// Gets or sets the design-time background color.
    /// </summary>
    [Reactive]
    public string DesignBackground { get; set; } = "White";

    /// <summary>
    /// Command to zoom in.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }

    /// <summary>
    /// Command to zoom out.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }

    /// <summary>
    /// Command to reset zoom.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ResetZoomCommand { get; }

    /// <summary>
    /// Command to delete the selected items.
    /// </summary>
    public ReactiveCommand<Unit, Unit> DeleteSelectedCommand { get; }

    /// <summary>
    /// Fires when the design surface needs to rebuild (e.g., after AST changes).
    /// </summary>
    public event Action? RebuildRequested;

    public DesignSurfaceViewModel()
    {
        ZoomInCommand = ReactiveCommand.Create(() => { Zoom = Math.Min(Zoom + 0.25, 4.0); });
        ZoomOutCommand = ReactiveCommand.Create(() => { Zoom = Math.Max(Zoom - 0.25, 0.25); });
        ResetZoomCommand = ReactiveCommand.Create(() => { Zoom = 1.0; });
        DeleteSelectedCommand = ReactiveCommand.Create(DeleteSelected);
    }

    /// <summary>
    /// Updates the design tree and lookup maps.
    /// </summary>
    public void SetDesignTree(
        DesignItem? root,
        IReadOnlyDictionary<Guid, DesignItem> itemMap,
        IReadOnlyDictionary<Control, DesignItem> controlMap)
    {
        RootItem = root;
        _itemMap = itemMap;
        _controlMap = controlMap;
    }

    /// <summary>
    /// Selects a design item by AST node id if available.
    /// </summary>
    public void SelectByAstNodeId(Guid nodeId)
    {
        if (_itemMap.TryGetValue(nodeId, out DesignItem? item) && item is not null)
        {
            Selection.Select(item);
        }
        else
        {
            Selection.ClearSelection();
        }
    }

    /// <summary>
    /// Requests a full rebuild of the design surface from the AST.
    /// </summary>
    public void RequestRebuild()
    {
        RebuildRequested?.Invoke();
    }

    private void DeleteSelected()
    {
        // Delete selected items from the AST
        IReadOnlyList<IDesignItem> selected = Selection.SelectedItems;
        foreach (IDesignItem item in selected.ToList())
        {
            if (item is DesignItem di && di.AstNode.Parent is MutableAstObjectNode parentObj)
            {
                parentObj.Children.Remove(di.AstNode);
            }
        }
        Selection.ClearSelection();
        RequestRebuild();
    }
}
