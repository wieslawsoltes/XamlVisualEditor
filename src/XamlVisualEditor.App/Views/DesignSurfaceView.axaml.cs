using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using XamlVisualEditor.Core;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.Designer.DragDrop;
using XamlVisualEditor.Designer.Rendering;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the design surface view.
/// Wires the DesignSurfaceViewModel.RebuildRequested event to re-create the
/// control tree from the current AST using ControlFactory.
/// Handles drag-and-drop from the toolbox to create new controls.
/// Defers rebuild until the canvas panel is attached to the visual tree.
/// </summary>
public sealed partial class DesignSurfaceView : UserControl
{
    private DesignSurfaceViewModel? _currentVm;
    private bool _isLoaded;
    private bool _rebuildPending;
    private Panel? _canvas;
    private Control? _rootControl;

    public DesignSurfaceView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;

        // Wire drag-and-drop handlers (listen even if handled by inner controls)
        AddHandler(
            DragDrop.DragOverEvent,
            OnDragOver,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
        AddHandler(
            DragDrop.DropEvent,
            OnDrop,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoaded = true;

        _canvas = this.FindControl<Panel>("DesignCanvas");
        if (_canvas is not null)
        {
            _canvas.PointerPressed += OnCanvasPointerPressed;
        }

        // If a rebuild was requested before we were loaded, do it now
        if (_rebuildPending)
        {
            _rebuildPending = false;
            ExecuteRebuild();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous VM
        if (_currentVm is not null)
        {
            _currentVm.RebuildRequested -= OnRebuildRequested;
        }

        _currentVm = DataContext as DesignSurfaceViewModel;

        if (_currentVm is not null)
        {
            _currentVm.RebuildRequested += OnRebuildRequested;

            // Trigger an initial rebuild if we're already loaded
            if (_isLoaded)
            {
                ExecuteRebuild();
            }
            else
            {
                _rebuildPending = true;
            }
        }
    }

    private void OnRebuildRequested()
    {
        if (!_isLoaded)
        {
            _rebuildPending = true;
            return;
        }

        // Ensure we run on the UI thread (sync events may come from background)
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ExecuteRebuild);
            return;
        }

        ExecuteRebuild();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DesignerDataFormats.ToolboxItem))
        {
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DesignerDataFormats.ToolboxItem))
        {
            return;
        }

        string? typeName = e.DataTransfer.TryGetValue(DesignerDataFormats.ToolboxItem);
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return;
        }

        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        if (docVm is null)
        {
            return;
        }

        MutableAstDocument? doc = docVm.SyncEngine.CurrentDocument;
        if (doc?.Root is null)
        {
            return;
        }

        UpdateCanvasSizeFromRoot(docVm, doc.Root);

        // Create a new AST node for the dropped control
        MutableAstObjectNode newNode = new()
        {
            TypeName = typeName,
            XmlNamespace = "https://github.com/avaloniaui"
        };

        // Add to root or the first panel-like child
        MutableAstObjectNode targetParent = FindBestDropTarget(doc.Root);
        targetParent.Children.Add(newNode);

        // Notify the sync engine that the AST changed (from design surface)
        docVm.SyncEngine.NotifyAstChanged(doc, SyncSource.DesignSurface);

        e.Handled = true;
    }

    /// <summary>
    /// Finds the best parent node to drop a new control into.
    /// Prefers the first Panel/Grid/StackPanel/DockPanel child of the root,
    /// otherwise falls back to the root itself.
    /// </summary>
    private static MutableAstObjectNode FindBestDropTarget(MutableAstObjectNode root)
    {
        // Check root's children for layout panels
        foreach (MutableAstNode child in root.Children)
        {
            if (child is MutableAstObjectNode objChild &&
                IsLayoutPanel(objChild.TypeName))
            {
                return objChild;
            }
        }

        // If root itself is a layout panel, use it
        if (IsLayoutPanel(root.TypeName))
        {
            return root;
        }

        // Default: add to root (e.g., UserControl)
        return root;
    }

    private static bool IsLayoutPanel(string typeName)
    {
        return typeName is "Grid" or "StackPanel" or "DockPanel" or "WrapPanel"
            or "Canvas" or "Panel" or "UniformGrid" or "RelativePanel";
    }

    private void ExecuteRebuild()
    {
        Panel? canvas = _canvas ?? this.FindControl<Panel>("DesignCanvas");
        if (canvas is null)
        {
            return;
        }

        canvas.Children.Clear();

        // Walk up to find the DesignerDocumentViewModel that owns the SyncEngine & ControlFactory
        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        if (docVm is null)
        {
            return;
        }

        MutableAstDocument? doc = docVm.SyncEngine.CurrentDocument;
        if (doc?.Root is null)
        {
            return;
        }

        Control? tree = docVm.ControlFactory.CreateControlTree(doc.Root);
        if (tree is null)
        {
            return;
        }

        canvas.Children.Add(tree);
        _rootControl = tree;

        // Build design item maps for selection sync
        Dictionary<Guid, DesignItem> itemMap = new();
        Dictionary<Control, DesignItem> controlMap = new();
        DesignItem rootItem = BuildDesignItemTree(doc.Root, tree, itemMap, controlMap);
        _currentVm?.SetDesignTree(rootItem, itemMap, controlMap);
    }

    private void UpdateCanvasSizeFromRoot(DesignerDocumentViewModel docVm, MutableAstObjectNode root)
    {
        if (_currentVm is null)
        {
            return;
        }

        double? designWidth = GetNumericProperty(root, "DesignWidth");
        double? designHeight = GetNumericProperty(root, "DesignHeight");

        if (!designWidth.HasValue || !designHeight.HasValue)
        {
            TryGetDesignSizeFromText(docVm.SyncEngine.CurrentText, ref designWidth, ref designHeight);
        }

        if (!designWidth.HasValue)
        {
            designWidth = GetNumericProperty(root, "Width");
        }

        if (!designHeight.HasValue)
        {
            designHeight = GetNumericProperty(root, "Height");
        }

        _currentVm.CanvasWidth = designWidth ?? 800;
        _currentVm.CanvasHeight = designHeight ?? 600;
    }

    private static void TryGetDesignSizeFromText(string? text, ref double? designWidth, ref double? designHeight)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!designWidth.HasValue)
        {
            designWidth = TryMatchDesignDimension(text, "DesignWidth");
        }

        if (!designHeight.HasValue)
        {
            designHeight = TryMatchDesignDimension(text, "DesignHeight");
        }
    }

    private static double? TryMatchDesignDimension(string text, string propertyName)
    {
        Regex regex = new($"\\b(?:\\w+:)?{propertyName}\\s*=\\s*\"(?<value>[0-9]+(?:\\.[0-9]+)?)\"", RegexOptions.IgnoreCase);
        Match match = regex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        string value = match.Groups["value"].Value;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : null;
    }

    private static double? GetNumericProperty(MutableAstObjectNode node, string propertyName)
    {
        foreach (MutableAstPropertyNode prop in node.Properties)
        {
            if (!IsPropertyNameMatch(prop.PropertyName, propertyName))
            {
                continue;
            }

            if (prop.Value is MutableAstTextNode textNode &&
                double.TryParse(textNode.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsPropertyNameMatch(string propertyName, string target)
    {
        if (string.Equals(propertyName, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int colonIndex = propertyName.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < propertyName.Length - 1)
        {
            string suffix = propertyName[(colonIndex + 1)..];
            return string.Equals(suffix, target, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_currentVm is null || _rootControl is null)
        {
            return;
        }

        Point position = e.GetPosition(_rootControl);
        Control? hit = ControlFactory.HitTest(_rootControl, position);
        if (hit is not null && _currentVm.ControlMap.TryGetValue(hit, out DesignItem? item) && item is not null)
        {
            _currentVm.Selection.Select(item);
            DesignerDocumentViewModel? docVm = FindDocumentViewModel();
            if (docVm is not null)
            {
                docVm.SelectedNodeId = item.AstNodeId;
            }
            return;
        }

        _currentVm.Selection.ClearSelection();
        DesignerDocumentViewModel? docVmClear = FindDocumentViewModel();
        if (docVmClear is not null)
        {
            docVmClear.SelectedNodeId = null;
        }
    }

    private static DesignItem BuildDesignItemTree(
        MutableAstObjectNode astNode,
        Control control,
        Dictionary<Guid, DesignItem> itemMap,
        Dictionary<Control, DesignItem> controlMap)
    {
        DesignItem item = new(astNode, control);
        itemMap[astNode.Id] = item;
        controlMap[control] = item;

        if (control is Panel panel)
        {
            List<MutableAstObjectNode> astChildren = astNode.Children
                .OfType<MutableAstObjectNode>()
                .ToList();

            int count = Math.Min(astChildren.Count, panel.Children.Count);
            for (int i = 0; i < count; i++)
            {
                if (panel.Children[i] is Control childControl)
                {
                    DesignItem childItem = BuildDesignItemTree(astChildren[i], childControl, itemMap, controlMap);
                    item.AddChild(childItem);
                }
            }
        }
        else if (control is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            MutableAstObjectNode? astChild = astNode.Children.OfType<MutableAstObjectNode>().FirstOrDefault();
            if (astChild is not null)
            {
                DesignItem childItem = BuildDesignItemTree(astChild, decoratorChild, itemMap, controlMap);
                item.AddChild(childItem);
            }
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            MutableAstObjectNode? astChild = astNode.Children.OfType<MutableAstObjectNode>().FirstOrDefault();
            if (astChild is not null)
            {
                DesignItem childItem = BuildDesignItemTree(astChild, contentChild, itemMap, controlMap);
                item.AddChild(childItem);
            }
        }
        else if (control is ItemsControl itemsControl)
        {
            List<MutableAstObjectNode> astChildren = astNode.Children
                .OfType<MutableAstObjectNode>()
                .ToList();

            IList? items = itemsControl.ItemsSource as IList;
            if (items is not null)
            {
                int count = Math.Min(astChildren.Count, items.Count);
                for (int i = 0; i < count; i++)
                {
                    if (items[i] is Control childControl)
                    {
                        DesignItem childItem = BuildDesignItemTree(astChildren[i], childControl, itemMap, controlMap);
                        item.AddChild(childItem);
                    }
                }
            }
        }

        return item;
    }

    private DesignerDocumentViewModel? FindDocumentViewModel()
    {
        // The DesignSurfaceView's DataContext is a DesignSurfaceViewModel.
        // Its parent DesignerDocumentViewModel is accessible via the visual tree
        // since DesignerDocumentView contains this view.
        Control? current = this.Parent as Control;
        while (current is not null)
        {
            if (current.DataContext is DesignerDocumentViewModel dvm)
            {
                return dvm;
            }

            current = current.Parent as Control;
        }

        return null;
    }
}
