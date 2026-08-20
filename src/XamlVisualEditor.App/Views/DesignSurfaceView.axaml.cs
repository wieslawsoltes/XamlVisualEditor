using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using System.Reactive;
using System.Windows.Input;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Core;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.Designer.DragDrop;
using XamlVisualEditor.Designer.Rendering;
using XamlVisualEditor.Designer.Adorners;
using XamlVisualEditor.Shell;
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
    private Control? _zoomSurface;
    private TopLevel? _topLevel;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8.0;
    private DesignAdornerLayer? _adornerLayer;
    private ScrollViewer? _scrollViewer;
    private Border? _surfaceBorder;
    private int _appliedWorkspaceThemeVersion = -1;
    private readonly List<IStyle> _workspaceThemeStyles = new();
    private readonly List<IResourceProvider> _workspaceThemeResources = new();
    private RulerControl? _horizontalRuler;
    private RulerControl? _verticalRuler;
    private IDesignItem? _hoveredItem;
    private DragState? _dragState;
    private MarqueeState? _marqueeState;
    private bool _isSpaceDown;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panStartOffset;
    private readonly SurfaceDropHandler _surfaceDropHandler = new();
    private NotifyCollectionChangedEventHandler? _guideChangedHandler;
    private readonly ContextMenu _surfaceContextMenu;
    private readonly MenuItem _openDefinitionMenuItem;

    public DesignSurfaceView()
    {
        InitializeComponent();

        _openDefinitionMenuItem = new MenuItem { Header = "Open Definition" };
        _openDefinitionMenuItem.Click += OnOpenDefinitionClick;
        _surfaceContextMenu = new ContextMenu();
        _surfaceContextMenu.Items.Add(_openDefinitionMenuItem);

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

        AddHandler(
            KeyDownEvent,
            OnKeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
        AddHandler(
            KeyUpEvent,
            OnKeyUp,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);

        AddHandler(
            PointerWheelChangedEvent,
            OnPointerWheelChanged,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);

        DetachedFromLogicalTree += OnDetachedFromLogicalTree;
    }

    private void OnDetachedFromLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(KeyDownEvent, OnKeyDown);
        _topLevel.RemoveHandler(KeyUpEvent, OnKeyUp);
        _topLevel = null;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoaded = true;

        _canvas = this.FindControl<Panel>("DesignCanvas");
        _zoomSurface = this.FindControl<Control>("ZoomSurface");
        _adornerLayer = this.FindControl<DesignAdornerLayer>("AdornerLayer");
        _scrollViewer = this.FindControl<ScrollViewer>("SurfaceScrollViewer");
        _surfaceBorder = this.FindControl<Border>("SurfaceBorder");
        _horizontalRuler = this.FindControl<RulerControl>("HorizontalRuler");
        _verticalRuler = this.FindControl<RulerControl>("VerticalRuler");
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
        _topLevel?.AddHandler(KeyUpEvent, OnKeyUp,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
        if (_canvas is not null)
        {
            _canvas.PointerPressed += OnCanvasPointerPressed;
            _canvas.PointerMoved += OnCanvasPointerMoved;
            _canvas.PointerReleased += OnCanvasPointerReleased;
            _canvas.PointerCaptureLost += OnCanvasPointerCaptureLost;
            _canvas.ContextMenu = _surfaceContextMenu;
        }

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnSurfaceScrollChanged;
            _scrollViewer.SizeChanged += OnSurfaceSizeChanged;
            UpdateSurfaceMargin();
            UpdateRulerOffsets();
        }

        if (_adornerLayer is not null && _canvas is not null)
        {
            _adornerLayer.SetSurfaceRoot(GetSurfaceRoot());
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
            _currentVm.Selection.SelectionChanged -= OnSelectionChanged;
            _currentVm.PropertyChanged -= OnDesignSurfacePropertyChanged;
            DetachGuideHandlers(_currentVm);
        }

        _currentVm = DataContext as DesignSurfaceViewModel;

        if (_currentVm is not null)
        {
            _currentVm.RebuildRequested += OnRebuildRequested;
            _currentVm.Selection.SelectionChanged += OnSelectionChanged;
            _currentVm.PropertyChanged += OnDesignSurfacePropertyChanged;
            AttachGuideHandlers(_currentVm);
            ApplyAdornerVisibility();
            UpdateGuideLines();
            UpdateRulerSelectionBounds();

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

        UpdateNavigateMenuState();
    }

    private void OnSelectionChanged(IReadOnlyList<IDesignItem> selected)
    {
        Guid? selectedId = null;
        if (_currentVm?.Selection.PrimarySelection is DesignItem primary)
        {
            selectedId = primary.AstNodeId;
        }

        if (_currentVm?.IsSelectionSyncing != true)
        {
            UpdateSelectedNode(selectedId);
        }

        if (_adornerLayer is null)
        {
            return;
        }

        _adornerLayer.UpdateSelection(selected);
        UpdateRulerSelectionBounds();
        UpdateSpacingGuidesForSelection();
        UpdateNavigateMenuState();
    }

    private void OnDesignSurfacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignSurfaceViewModel.IsEditMode))
        {
            ApplyEditMode();
            return;
        }

        if (e.PropertyName == nameof(DesignSurfaceViewModel.ShowMarginPadding) ||
            e.PropertyName == nameof(DesignSurfaceViewModel.ShowAlignmentGuides) ||
            e.PropertyName == nameof(DesignSurfaceViewModel.ShowGuides) ||
            e.PropertyName == nameof(DesignSurfaceViewModel.ShowSpacingGuides))
        {
            ApplyAdornerVisibility();
            UpdateGuideLines();
            UpdateSpacingGuidesForSelection();
            return;
        }

        if (e.PropertyName == nameof(DesignSurfaceViewModel.Zoom) ||
            e.PropertyName == nameof(DesignSurfaceViewModel.CanvasWidth) ||
            e.PropertyName == nameof(DesignSurfaceViewModel.CanvasHeight))
        {
            UpdateSurfaceMargin();
            UpdateRulerOffsets();
            UpdateRulerSelectionBounds();
            RefreshSelectionAdorners();
        }
    }

    private void OnSurfaceSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateSurfaceMargin();
        UpdateRulerOffsets();
        RefreshSelectionAdorners();
    }

    private void AttachGuideHandlers(DesignSurfaceViewModel vm)
    {
        _guideChangedHandler ??= (_, _) => UpdateGuideLines();
        vm.HorizontalGuides.CollectionChanged += _guideChangedHandler;
        vm.VerticalGuides.CollectionChanged += _guideChangedHandler;
    }

    private void DetachGuideHandlers(DesignSurfaceViewModel vm)
    {
        if (_guideChangedHandler is null)
        {
            return;
        }

        vm.HorizontalGuides.CollectionChanged -= _guideChangedHandler;
        vm.VerticalGuides.CollectionChanged -= _guideChangedHandler;
    }

    private void ApplyAdornerVisibility()
    {
        if (_adornerLayer is null || _currentVm is null)
        {
            return;
        }

        _adornerLayer.ShowMarginPadding = _currentVm.ShowMarginPadding;
        _adornerLayer.ShowAlignmentGuides = _currentVm.ShowAlignmentGuides;
        _adornerLayer.ShowGuides = _currentVm.ShowGuides;
        _adornerLayer.ShowSpacingGuides = _currentVm.ShowSpacingGuides;
    }

    private void UpdateGuideLines()
    {
        if (_adornerLayer is null || _currentVm is null)
        {
            return;
        }

        if (_currentVm.ShowGuides)
        {
            _adornerLayer.UpdateGuideLines(_currentVm.HorizontalGuides, _currentVm.VerticalGuides);
        }
        else
        {
            _adornerLayer.UpdateGuideLines(Array.Empty<double>(), Array.Empty<double>());
        }
    }

    private void OnSurfaceScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateRulerOffsets();
        RefreshSelectionAdorners();
    }

    private void UpdateRulerOffsets()
    {
        if (_scrollViewer is null)
        {
            return;
        }

        Point origin = GetCanvasOriginInScrollViewer();
        double offsetX = -origin.X;
        double offsetY = -origin.Y;

        if (_horizontalRuler is not null)
        {
            _horizontalRuler.Offset = offsetX;
        }

        if (_verticalRuler is not null)
        {
            _verticalRuler.Offset = offsetY;
        }
    }

    private Point GetCanvasOriginInScrollViewer()
    {
        if (_scrollViewer is null)
        {
            return default;
        }

        Control? surfaceRoot = GetSurfaceRoot();
        Point? origin = surfaceRoot?.TranslatePoint(new Point(0, 0), _scrollViewer);
        return origin ?? default;
    }

    private void UpdateRulerSelectionBounds()
    {
        if (_canvas is null)
        {
            return;
        }

        Rect? selectionBounds = GetSelectionBounds();
        if (_horizontalRuler is not null)
        {
            _horizontalRuler.SelectionStart = selectionBounds?.Left;
            _horizontalRuler.SelectionEnd = selectionBounds?.Right;
        }

        if (_verticalRuler is not null)
        {
            _verticalRuler.SelectionStart = selectionBounds?.Top;
            _verticalRuler.SelectionEnd = selectionBounds?.Bottom;
        }
    }

    private Rect? GetSelectionBounds()
    {
        if (_currentVm is null || _currentVm.Selection.SelectedItems.Count == 0)
        {
            return null;
        }

        Control? surfaceRoot = GetSurfaceRoot();
        Rect? bounds = null;
        foreach (IDesignItem item in _currentVm.Selection.SelectedItems)
        {
            if (item is not DesignItem designItem)
            {
                continue;
            }

            Rect itemBounds = designItem.GetBoundsRelativeTo(surfaceRoot);
            bounds = bounds is null ? itemBounds : bounds.Value.Union(itemBounds);
        }

        return bounds;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _isSpaceDown = true;
            UpdatePanCursor();
            e.Handled = true;
            return;
        }

        if (_currentVm is null)
        {
            return;
        }

        if (HandleZoomShortcuts(e))
        {
            return;
        }

        if (!_currentVm.IsEditMode)
        {
            return;
        }

        if (_currentVm.Selection.PrimarySelection is not DesignItem primary)
        {
            return;
        }

        bool resize = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        double step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;

        switch (e.Key)
        {
            case Key.Left:
                if (resize)
                {
                    ResizeSelection(primary, -step, 0);
                }
                else
                {
                    NudgeSelection(-step, 0);
                }
                e.Handled = true;
                break;
            case Key.Right:
                if (resize)
                {
                    ResizeSelection(primary, step, 0);
                }
                else
                {
                    NudgeSelection(step, 0);
                }
                e.Handled = true;
                break;
            case Key.Up:
                if (resize)
                {
                    ResizeSelection(primary, 0, -step);
                }
                else
                {
                    NudgeSelection(0, -step);
                }
                e.Handled = true;
                break;
            case Key.Down:
                if (resize)
                {
                    ResizeSelection(primary, 0, step);
                }
                else
                {
                    NudgeSelection(0, step);
                }
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
        {
            return;
        }

        _isSpaceDown = false;
        UpdatePanCursor();
        if (_isPanning)
        {
            EndPan();
        }
        e.Handled = true;
    }

    private bool HandleZoomShortcuts(KeyEventArgs e)
    {
        if (_currentVm is null || _scrollViewer is null)
        {
            return false;
        }

        bool zoomModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control)
            || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (zoomModifier)
        {
            Point center = GetViewportCenter();
            if (e.Key is Key.OemPlus or Key.Add)
            {
                if (TryGetCanvasFocus(center, out Point canvasFocus))
                {
                    ApplyZoom(ClampZoom(_currentVm.Zoom * 1.1), center, canvasFocus);
                }
                else
                {
                    ApplyZoom(ClampZoom(_currentVm.Zoom * 1.1), center);
                }
                e.Handled = true;
                return true;
            }

            if (e.Key is Key.OemMinus or Key.Subtract)
            {
                if (TryGetCanvasFocus(center, out Point canvasFocus))
                {
                    ApplyZoom(ClampZoom(_currentVm.Zoom / 1.1), center, canvasFocus);
                }
                else
                {
                    ApplyZoom(ClampZoom(_currentVm.Zoom / 1.1), center);
                }
                e.Handled = true;
                return true;
            }
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (e.Key == Key.D1)
            {
                ZoomToFit();
                e.Handled = true;
                return true;
            }

            if (e.Key == Key.D2)
            {
                ZoomToSelection();
                e.Handled = true;
                return true;
            }

            if (e.Key == Key.D0)
            {
                Point center = GetViewportCenter();
                if (TryGetCanvasFocus(center, out Point canvasFocus))
                {
                    ApplyZoom(1.0, center, canvasFocus);
                }
                else
                {
                    ApplyZoom(1.0, center);
                }
                e.Handled = true;
                return true;
            }
        }

        return false;
    }

    private void OnZoomToFitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ZoomToFit();
    }

    private void OnZoomToSelectionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ZoomToSelection();
    }

    private void ZoomToSelection()
    {
        Rect? selectionBounds = GetSelectionBounds();
        if (selectionBounds is null || selectionBounds.Value.Width <= 0 || selectionBounds.Value.Height <= 0)
        {
            ZoomToFit();
            return;
        }

        ZoomToBounds(selectionBounds.Value);
    }

    private void ZoomToFit()
    {
        if (_currentVm is null)
        {
            return;
        }

        Rect bounds = new(0, 0, _currentVm.CanvasWidth, _currentVm.CanvasHeight);
        ZoomToBounds(bounds);
    }

    private void ZoomToBounds(Rect bounds)
    {
        if (_currentVm is null || _scrollViewer is null)
        {
            return;
        }

        Size viewport = _scrollViewer.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        const double padding = 0;
        double width = Math.Max(1, bounds.Width);
        double height = Math.Max(1, bounds.Height);
        double zoomX = (viewport.Width - padding * 2) / width;
        double zoomY = (viewport.Height - padding * 2) / height;
        double targetZoom = ClampZoom(Math.Min(zoomX, zoomY));
        _currentVm.Zoom = targetZoom;
        UpdateSurfaceMargin();

        Point center = bounds.Center;
        Point origin = GetCanvasOriginInScrollViewer();
        Vector targetOffset = new(
            origin.X + center.X * targetZoom - viewport.Width / 2,
            origin.Y + center.Y * targetZoom - viewport.Height / 2);

        Dispatcher.UIThread.Post(() =>
        {
            if (_scrollViewer is null)
            {
                return;
            }

            _scrollViewer.Offset = targetOffset;
        }, DispatcherPriority.Render);
    }

    private void ApplyZoom(double newZoom, Point focusInScrollViewer)
    {
        ApplyZoom(newZoom, focusInScrollViewer, null);
    }

    private void ApplyZoom(double newZoom, Point focusInScrollViewer, Point? canvasFocus)
    {
        if (_currentVm is null || _scrollViewer is null || _canvas is null)
        {
            return;
        }

        double clamped = ClampZoom(newZoom);
        double oldZoom = _currentVm.Zoom;
        if (Math.Abs(oldZoom - clamped) < 0.0001 || oldZoom <= 0)
        {
            return;
        }

        Vector focus = new(focusInScrollViewer.X, focusInScrollViewer.Y);
        Vector offset = _scrollViewer.Offset;
        double scale = clamped / oldZoom;

        _currentVm.Zoom = clamped;
        UpdateSurfaceMargin();

        Dispatcher.UIThread.Post(() =>
        {
            if (_scrollViewer is null)
            {
                return;
            }

            if (canvasFocus.HasValue)
            {
                Point logical = canvasFocus.Value;
                Point origin = GetCanvasOriginInScrollViewer();
                _scrollViewer.Offset = new Vector(
                    origin.X + logical.X * clamped - focus.X,
                    origin.Y + logical.Y * clamped - focus.Y);
            }
            else
            {
                _scrollViewer.Offset = (offset + focus) * scale - focus;
            }
        }, DispatcherPriority.Render);
    }

    private void UpdateSurfaceMargin()
    {
        if (_currentVm is null || _scrollViewer is null || _surfaceBorder is null)
        {
            return;
        }

        Size viewport = _scrollViewer.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        double zoom = Math.Max(0.01, _currentVm.Zoom);
        double contentWidth = _currentVm.CanvasWidth * zoom;
        double contentHeight = _currentVm.CanvasHeight * zoom;
        double marginX = Math.Max(0, (viewport.Width - contentWidth) / 2);
        double marginY = Math.Max(0, (viewport.Height - contentHeight) / 2);
        _surfaceBorder.Margin = new Thickness(marginX, marginY, marginX, marginY);
    }

    private bool TryGetCanvasFocus(Point focusInScrollViewer, out Point canvasFocus)
    {
        canvasFocus = default;
        if (_scrollViewer is null || _currentVm is null)
        {
            return false;
        }

        Point origin = GetCanvasOriginInScrollViewer();
        double zoom = Math.Max(0.01, _currentVm.Zoom);
        Vector offset = _scrollViewer.Offset;
        double logicalX = (focusInScrollViewer.X + offset.X - origin.X) / zoom;
        double logicalY = (focusInScrollViewer.Y + offset.Y - origin.Y) / zoom;
        canvasFocus = new Point(logicalX, logicalY);
        return true;
    }

    // Intentionally left without coordinate helpers; use e.GetPosition(target)
    // for consistent hit testing across split layouts.

    private Control? GetSurfaceRoot()
    {
        return _canvas;
    }

    private Point GetViewportCenter()
    {
        if (_scrollViewer is null)
        {
            return default;
        }

        return new Point(_scrollViewer.Viewport.Width / 2, _scrollViewer.Viewport.Height / 2);
    }

    private static double ClampZoom(double zoom)
    {
        return Math.Clamp(zoom, MinZoom, MaxZoom);
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
        if (_currentVm is null || !_currentVm.IsEditMode)
        {
            return;
        }

        if (!e.DataTransfer.Contains(DesignerDataFormats.ToolboxItem))
        {
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_currentVm is null || !_currentVm.IsEditMode)
        {
            return;
        }

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

        _zoomSurface ??= this.FindControl<Control>("ZoomSurface");

        canvas.Children.Clear();

        // Walk up to find the DesignerDocumentViewModel that owns the SyncEngine & ControlFactory
        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        if (docVm is null)
        {
            return;
        }

        List<Guid>? selectedIds = null;
        if (_currentVm is not null)
        {
            selectedIds = _currentVm.Selection.SelectedItems
                .OfType<DesignItem>()
                .Select(item => item.AstNodeId)
                .ToList();
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

        ApplyDesignModeProperties(tree);
        ApplyWorkspaceThemes(canvas);

        canvas.Children.Add(tree);
        _rootControl = tree;

        ApplyEditMode();

        if (_adornerLayer is not null)
        {
            _adornerLayer.SetSurfaceRoot(GetSurfaceRoot());
            _adornerLayer.UpdateSelection(_currentVm?.Selection.SelectedItems ?? Array.Empty<IDesignItem>());
            ApplyAdornerVisibility();
            UpdateGuideLines();
        }

        UpdateRulerSelectionBounds();

        // Build design item maps for selection sync
        Dictionary<Guid, DesignItem> itemMap = new();
        Dictionary<Control, DesignItem> controlMap = new();
        DesignItem rootItem = BuildDesignItemTree(doc.Root, tree, itemMap, controlMap);
        _currentVm?.SetDesignTree(rootItem, itemMap, controlMap);

        if (_currentVm is not null)
        {
            Guid? selectedId = docVm.SelectedNodeId;
            if (selectedId is not null)
            {
                _currentVm.SelectByAstNodeIdFromSync(selectedId.Value);
            }
            else if (selectedIds is { Count: > 0 })
            {
                _currentVm.Selection.ClearSelection();
                foreach (Guid id in selectedIds)
                {
                    if (_currentVm.ItemMap.TryGetValue(id, out DesignItem? item) && item is not null)
                    {
                        _currentVm.Selection.Select(item, addToSelection: true);
                    }
                }
            }
        }
    }

    private void ApplyEditMode()
    {
        if (_currentVm is null || _rootControl is null)
        {
            return;
        }

        bool isEditMode = _currentVm.IsEditMode;
        bool interactive = !isEditMode;
        SetPreviewHitTest(_rootControl, interactive);

        if (_canvas is not null)
        {
            _canvas.ContextMenu = isEditMode ? _surfaceContextMenu : null;
        }

        if (interactive)
        {
            _dragState = null;
            _marqueeState = null;
            _hoveredItem = null;
            _isPanning = false;
            _isSpaceDown = false;
            _adornerLayer?.ClearAll();
            _currentVm.Selection.ClearSelection();
            UpdateSelectedNode(null);
            UpdatePanCursor();
        }
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
        if (_currentVm is null || _rootControl is null || _canvas is null)
        {
            return;
        }

        Focus();

        if (TryStartPan(e))
        {
            e.Handled = true;
            return;
        }

        if (!_currentVm.IsEditMode)
        {
            return;
        }

        PointerPoint pointInfo = e.GetCurrentPoint(_canvas);
        if (!pointInfo.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_adornerLayer is not null)
        {
            ResizeHandle? handle = _adornerLayer.HitTestResizeHandles(e.GetPosition(_adornerLayer));
            if (handle is not null && _currentVm.Selection.PrimarySelection is DesignItem primary)
            {
                StartResize(primary, handle, e.GetPosition(_canvas));
                e.Pointer.Capture(_canvas);
                e.Handled = true;
                return;
            }
        }

        Point canvasPoint = e.GetPosition(_canvas);
        Point rootPoint = e.GetPosition(_rootControl);
        Control? hit = ControlFactory.HitTest(_rootControl, rootPoint);
        if (hit is not null && _currentVm.ControlMap.TryGetValue(hit, out DesignItem? item) && item is not null)
        {
            ApplySelection(item, e.KeyModifiers);
            UpdateSelectedNode(item.AstNodeId);
            if (e.ClickCount == 2)
            {
                DesignerDocumentViewModel? docVm = FindDocumentViewModel();
                if (docVm is not null)
                {
                    _ = docVm.NavigateToDefinitionAsync();
                }

                e.Handled = true;
                return;
            }
            StartDrag(item, canvasPoint);
            e.Pointer.Capture(_canvas);
            e.Handled = true;
            return;
        }

        _currentVm.Selection.ClearSelection();
        UpdateSelectedNode(null);
        StartMarquee(canvasPoint, e.KeyModifiers);
        e.Pointer.Capture(_canvas);
    }

    private void OnOpenDefinitionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        if (docVm is null)
        {
            return;
        }

        _ = docVm.NavigateToDefinitionAsync();
    }

    private void UpdateNavigateMenuState()
    {
        if (_openDefinitionMenuItem is null)
        {
            return;
        }

        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        _openDefinitionMenuItem.IsEnabled = docVm is not null
            && ((ICommand)docVm.NavigateToDefinitionCommand).CanExecute(null);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_currentVm is null || _rootControl is null || _canvas is null)
        {
            return;
        }

        if (_isPanning)
        {
            UpdatePan(e);
            return;
        }

        if (!_currentVm.IsEditMode)
        {
            return;
        }

        if (_dragState is not null)
        {
            UpdateDragState(e.GetPosition(_canvas));
            return;
        }

        if (_marqueeState is not null)
        {
            UpdateMarquee(e.GetPosition(_canvas));
            return;
        }

        UpdateHover(e.GetPosition(_rootControl));
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_currentVm is null || _canvas is null)
        {
            return;
        }

        if (_isPanning)
        {
            EndPan();
            e.Pointer.Capture(null);
            return;
        }

        if (_dragState is null)
        {
            if (_marqueeState is not null)
            {
                CommitMarqueeSelection();
                _marqueeState = null;
                e.Pointer.Capture(null);
            }
            return;
        }

        CommitDragState();
        _dragState = null;
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isPanning)
        {
            EndPan();
            return;
        }

        if (_dragState is null)
        {
            if (_marqueeState is not null)
            {
                CommitMarqueeSelection();
                _marqueeState = null;
            }
            return;
        }

        CommitDragState();
        _dragState = null;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_currentVm is null || _scrollViewer is null)
        {
            return;
        }

        bool zoomModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control)
            || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!zoomModifier)
        {
            return;
        }

        double factor = Math.Pow(1.2, e.Delta.Y);
        double targetZoom = ClampZoom(_currentVm.Zoom * factor);
        Point focus = e.GetPosition(_scrollViewer);
        if (TryGetCanvasFocus(focus, out Point canvasFocus))
        {
            ApplyZoom(targetZoom, focus, canvasFocus);
        }
        else
        {
            ApplyZoom(targetZoom, focus);
        }
        e.Handled = true;
    }

    private bool TryStartPan(PointerPressedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            return false;
        }

        PointerPoint pointInfo = e.GetCurrentPoint(_scrollViewer);
        bool middle = pointInfo.Properties.IsMiddleButtonPressed;
        bool spaceDrag = _isSpaceDown && pointInfo.Properties.IsLeftButtonPressed;
        if (!middle && !spaceDrag)
        {
            return false;
        }

        _isPanning = true;
        _panStart = e.GetPosition(_scrollViewer);
        _panStartOffset = _scrollViewer.Offset;
        e.Pointer.Capture(_canvas);
        UpdatePanCursor();
        return true;
    }

    private void UpdatePan(PointerEventArgs e)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        Point current = e.GetPosition(_scrollViewer);
        Vector delta = current - _panStart;
        _scrollViewer.Offset = new Vector(
            _panStartOffset.X - delta.X,
            _panStartOffset.Y - delta.Y);
        UpdateRulerOffsets();
        RefreshSelectionAdorners();
    }

    private void EndPan()
    {
        _isPanning = false;
        UpdatePanCursor();
    }

    private void UpdatePanCursor()
    {
        if (_canvas is null)
        {
            return;
        }

        if (_isPanning || _isSpaceDown)
        {
            _canvas.Cursor = new Cursor(StandardCursorType.Hand);
        }
        else
        {
            _canvas.Cursor = null;
        }
    }

    private void ApplySelection(DesignItem item, KeyModifiers modifiers)
    {
        bool toggle = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        bool add = modifiers.HasFlag(KeyModifiers.Shift);

        if (toggle)
        {
            _currentVm?.Selection.ToggleSelection(item);
            return;
        }

        _currentVm?.Selection.Select(item, addToSelection: add);
    }

    private void UpdateSelectedNode(Guid? nodeId)
    {
        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        if (docVm is not null)
        {
            docVm.SetSelectedNode(nodeId, SyncSource.DesignSurface);
        }
    }

    private void StartMarquee(Point startPoint, KeyModifiers modifiers)
    {
        _marqueeState = new MarqueeState
        {
            StartPoint = startPoint,
            CurrentPoint = startPoint,
            Additive = modifiers.HasFlag(KeyModifiers.Shift)
        };

        _adornerLayer?.UpdateSelectionBox(new Rect(startPoint, startPoint));
    }

    private void UpdateMarquee(Point currentPoint)
    {
        if (_marqueeState is null)
        {
            return;
        }

        _marqueeState.CurrentPoint = currentPoint;
        Rect box = GetMarqueeRect(_marqueeState.StartPoint, _marqueeState.CurrentPoint);
        _adornerLayer?.UpdateSelectionBox(box);
    }

    private void CommitMarqueeSelection()
    {
        if (_marqueeState is null || _currentVm is null || _canvas is null)
        {
            return;
        }

        Rect box = GetMarqueeRect(_marqueeState.StartPoint, _marqueeState.CurrentPoint);

        if (!_marqueeState.Additive)
        {
            _currentVm.Selection.ClearSelection();
        }

        Control? surfaceRoot = GetSurfaceRoot();
        foreach (DesignItem item in _currentVm.ItemMap.Values)
        {
            Rect bounds = item.GetBoundsRelativeTo(surfaceRoot);
            if (bounds.Intersects(box))
            {
                _currentVm.Selection.Select(item, addToSelection: true);
            }
        }

        _adornerLayer?.UpdateSelectionBox(null);
    }

    private static Rect GetMarqueeRect(Point start, Point end)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);
        return new Rect(x, y, w, h);
    }

    private void NudgeSelection(double dx, double dy)
    {
        if (_currentVm is null)
        {
            return;
        }

        bool changed = false;
        foreach (IDesignItem item in _currentVm.Selection.SelectedItems)
        {
            if (item is DesignItem designItem)
            {
                changed |= NudgeItem(designItem, dx, dy);
            }
        }

        if (changed)
        {
            DesignerDocumentViewModel? docVm = FindDocumentViewModel();
            if (docVm?.SyncEngine.CurrentDocument is not null)
            {
                docVm.SyncEngine.NotifyAstChanged(docVm.SyncEngine.CurrentDocument, SyncSource.DesignSurface);
            }
        }
    }

    private bool NudgeItem(DesignItem item, double dx, double dy)
    {
        if (item.VisualElement?.Parent is Canvas)
        {
            double left = GetAttachedDouble(item, "Canvas.Left") + dx;
            double top = GetAttachedDouble(item, "Canvas.Top") + dy;
            SetAttachedDouble(item, "Canvas.Left", left);
            SetAttachedDouble(item, "Canvas.Top", top);
            return true;
        }

        if (item.VisualElement?.Parent is Grid grid)
        {
            int row = GetAttachedInt(item, "Grid.Row");
            int column = GetAttachedInt(item, "Grid.Column");
            if (dy < 0)
            {
                row = Math.Max(0, row - 1);
            }
            else if (dy > 0)
            {
                row = Math.Min(grid.RowDefinitions.Count - 1, row + 1);
            }

            if (dx < 0)
            {
                column = Math.Max(0, column - 1);
            }
            else if (dx > 0)
            {
                column = Math.Min(grid.ColumnDefinitions.Count - 1, column + 1);
            }

            SetAttachedInt(item, "Grid.Row", row);
            SetAttachedInt(item, "Grid.Column", column);
            return true;
        }

        if (item.VisualElement?.Parent is DockPanel)
        {
            Avalonia.Controls.Dock dock = GetDockFromDirection(dx, dy);
            SetAttachedString(item, "DockPanel.Dock", dock.ToString());
            return true;
        }

        if (item.VisualElement?.Parent is StackPanel stack)
        {
            bool vertical = stack.Orientation == Orientation.Vertical;
            int offset = vertical ? Math.Sign(dy) : Math.Sign(dx);
            return MoveSibling(item, offset);
        }

        if (item.VisualElement?.Parent is WrapPanel or UniformGrid)
        {
            int offset = Math.Sign(dx != 0 ? dx : dy);
            return MoveSibling(item, offset);
        }

        if (item.VisualElement?.Parent is Panel)
        {
            Thickness margin = GetThickness(item, "Margin");
            Thickness updated = new(
                margin.Left + dx,
                margin.Top + dy,
                margin.Right,
                margin.Bottom);
            SetThickness(item, "Margin", updated);
            return true;
        }

        return false;
    }

    private static Avalonia.Controls.Dock GetDockFromDirection(double dx, double dy)
    {
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx < 0 ? Avalonia.Controls.Dock.Left : Avalonia.Controls.Dock.Right;
        }

        return dy < 0 ? Avalonia.Controls.Dock.Top : Avalonia.Controls.Dock.Bottom;
    }

    private bool MoveSibling(DesignItem item, int offset)
    {
        if (offset == 0)
        {
            return false;
        }

        if (item.Parent is not DesignItem parent)
        {
            return false;
        }

        ObservableCollection<MutableAstNode> siblings = parent.AstNode.Children;
        int index = siblings.IndexOf(item.AstNode);
        if (index < 0)
        {
            return false;
        }

        int nextIndex = Math.Clamp(index + offset, 0, siblings.Count - 1);
        if (nextIndex == index)
        {
            return false;
        }

        siblings.Move(index, nextIndex);
        return true;
    }

    private void ResizeSelection(DesignItem primary, double deltaWidth, double deltaHeight)
    {
        double width = GetPropertyDouble(primary, "Width");
        double height = GetPropertyDouble(primary, "Height");

        width = Math.Max(1, width + deltaWidth);
        height = Math.Max(1, height + deltaHeight);

        SetPropertyDouble(primary, "Width", width);
        SetPropertyDouble(primary, "Height", height);

        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        if (docVm?.SyncEngine.CurrentDocument is not null)
        {
            docVm.SyncEngine.NotifyAstChanged(docVm.SyncEngine.CurrentDocument, SyncSource.DesignSurface);
        }
    }

    private void UpdateHover(Point surfacePoint)
    {
        if (_currentVm is null)
        {
            return;
        }

        Control? hit = ControlFactory.HitTest(_rootControl!, surfacePoint);
        IDesignItem? hover = null;
        if (hit is not null && _currentVm.ControlMap.TryGetValue(hit, out DesignItem? item) && item is not null)
        {
            hover = item;
        }

        if (!ReferenceEquals(_hoveredItem, hover))
        {
            _hoveredItem = hover;
            _adornerLayer?.UpdateHover(_hoveredItem);
        }
    }

    private void RefreshSelectionAdorners()
    {
        if (_adornerLayer is null || _currentVm is null)
        {
            return;
        }

        _adornerLayer.UpdateSelection(_currentVm.Selection.SelectedItems);
        UpdateRulerSelectionBounds();
        UpdateSpacingGuidesForSelection();
    }

    private void StartDrag(DesignItem item, Point startPoint)
    {
        DragState state = new()
        {
            Item = item,
            StartPoint = startPoint,
            LastPoint = startPoint,
            Mode = GetDragMode(item),
            StartBounds = item.GetBoundsRelativeTo(GetSurfaceRoot()),
            StartCanvasLeft = GetAttachedDouble(item, "Canvas.Left"),
            StartCanvasTop = GetAttachedDouble(item, "Canvas.Top")
        };

        if (state.Mode == DragMode.Canvas && _currentVm is not null)
        {
            foreach (IDesignItem selection in _currentVm.Selection.SelectedItems)
            {
                if (selection is DesignItem selectedItem && selectedItem.VisualElement?.Parent is Canvas)
                {
                    double left = GetAttachedDouble(selectedItem, "Canvas.Left");
                    double top = GetAttachedDouble(selectedItem, "Canvas.Top");
                    state.StartPositions[selectedItem.AstNodeId] = new Point(left, top);
                }
            }

            if (!state.StartPositions.ContainsKey(item.AstNodeId))
            {
                state.StartPositions[item.AstNodeId] = new Point(state.StartCanvasLeft, state.StartCanvasTop);
            }
        }

        _dragState = state;
    }

    private void StartResize(DesignItem item, ResizeHandle handle, Point startPoint)
    {
        _dragState = new DragState
        {
            Item = item,
            StartPoint = startPoint,
            LastPoint = startPoint,
            Mode = DragMode.Resize,
            ResizeDirection = handle.Direction,
            StartBounds = item.GetBoundsRelativeTo(GetSurfaceRoot()),
            StartCanvasLeft = GetAttachedDouble(item, "Canvas.Left"),
            StartCanvasTop = GetAttachedDouble(item, "Canvas.Top")
        };
    }

    private void UpdateDragState(Point currentPoint)
    {
        if (_dragState is null || _currentVm is null)
        {
            return;
        }

        Vector delta = currentPoint - _dragState.StartPoint;
        if (!_dragState.HasMoved && delta.Length < 3)
        {
            return;
        }

        _dragState.HasMoved = true;
        _dragState.LastPoint = currentPoint;

        switch (_dragState.Mode)
        {
            case DragMode.Canvas:
                UpdateCanvasMove(_dragState, currentPoint);
                break;
            case DragMode.Grid:
                UpdateGridMove(_dragState, currentPoint);
                break;
            case DragMode.Dock:
                UpdateDockMove(_dragState, currentPoint);
                break;
            case DragMode.Reorder:
                UpdateReorderDrag(_dragState, currentPoint);
                break;
            case DragMode.Resize:
                UpdateResize(_dragState, currentPoint);
                break;
        }
    }

    private void CommitDragState()
    {
        if (_dragState is null || _currentVm is null)
        {
            return;
        }

        DesignerDocumentViewModel? docVm = FindDocumentViewModel();
        if (docVm is null)
        {
            return;
        }

        if (_dragState.Mode == DragMode.Reorder && _dragState.DropTarget is DesignItem target && _dragState.DropPosition.HasValue)
        {
            bool moved = _surfaceDropHandler.Move(_dragState.Item, target, _dragState.DropPosition.Value);
            if (moved && docVm.SyncEngine.CurrentDocument is not null)
            {
                docVm.SyncEngine.NotifyAstChanged(docVm.SyncEngine.CurrentDocument, SyncSource.DesignSurface);
            }
        }
        else if (_dragState.HasMoved && docVm.SyncEngine.CurrentDocument is not null)
        {
            docVm.SyncEngine.NotifyAstChanged(docVm.SyncEngine.CurrentDocument, SyncSource.DesignSurface);
        }

        _adornerLayer?.UpdateDropTarget(null, null);
        _adornerLayer?.UpdateSnapLines(Array.Empty<double>(), Array.Empty<double>());
        _adornerLayer?.UpdateSpacingGuides(Array.Empty<SpacingGuide>());
        RefreshSelectionAdorners();
    }

    private void UpdateCanvasMove(DragState state, Point currentPoint)
    {
        if (_currentVm is null)
        {
            return;
        }

        Vector delta = currentPoint - state.StartPoint;
        double left = state.StartCanvasLeft + delta.X;
        double top = state.StartCanvasTop + delta.Y;

        IReadOnlyList<double> snapHorizontal = Array.Empty<double>();
        IReadOnlyList<double> snapVertical = Array.Empty<double>();

        if (_currentVm.ShowAlignmentGuides || _currentVm.SnapLinesEnabled)
        {
            (left, top, snapHorizontal, snapVertical) = ApplySnapLines(state, left, top, _currentVm.SnapLinesEnabled);
        }

        if (_currentVm.ShowGuides || _currentVm.SnapToGuides)
        {
            (left, top) = ApplyGuideSnaps(state, left, top, _currentVm.SnapToGuides);
        }

        if (_currentVm.SnapToGrid)
        {
            left = Snap(left, _currentVm.GridSnapSize);
            top = Snap(top, _currentVm.GridSnapSize);
        }

        double dx = left - state.StartCanvasLeft;
        double dy = top - state.StartCanvasTop;

        if (state.StartPositions.Count > 0)
        {
            foreach ((Guid id, Point start) in state.StartPositions)
            {
                if (_currentVm.ItemMap.TryGetValue(id, out DesignItem? selectedItem) && selectedItem is not null)
                {
                    SetAttachedDouble(selectedItem, "Canvas.Left", start.X + dx);
                    SetAttachedDouble(selectedItem, "Canvas.Top", start.Y + dy);
                }
            }
        }
        else
        {
            SetAttachedDouble(state.Item, "Canvas.Left", left);
            SetAttachedDouble(state.Item, "Canvas.Top", top);
        }

        _adornerLayer?.UpdateSnapLines(snapHorizontal, snapVertical);
        UpdateSpacingGuidesForDrag(new Rect(left, top, state.StartBounds.Width, state.StartBounds.Height));
        RefreshSelectionAdorners();
    }

    private void UpdateGridMove(DragState state, Point currentPoint)
    {
        if (state.Item.VisualElement?.Parent is not Grid grid)
        {
            return;
        }

        Point inGrid = TranslateFromCanvas(grid, currentPoint);
        int row = FindGridIndex(grid.RowDefinitions, inGrid.Y, grid.Bounds.Height);
        int column = FindGridIndex(grid.ColumnDefinitions, inGrid.X, grid.Bounds.Width);

        SetAttachedInt(state.Item, "Grid.Row", row);
        SetAttachedInt(state.Item, "Grid.Column", column);
        RefreshSelectionAdorners();
    }

    private void UpdateDockMove(DragState state, Point currentPoint)
    {
        if (state.Item.VisualElement?.Parent is not DockPanel dockPanel)
        {
            return;
        }

        Point local = TranslateFromCanvas(dockPanel, currentPoint);
        Rect bounds = dockPanel.Bounds;
        double edge = Math.Min(bounds.Width, bounds.Height) * 0.2;

          Avalonia.Controls.Dock dock = Avalonia.Controls.Dock.Left;
        if (local.Y <= edge)
        {
             dock = Avalonia.Controls.Dock.Top;
        }
        else if (local.Y >= bounds.Height - edge)
        {
             dock = Avalonia.Controls.Dock.Bottom;
        }
        else if (local.X >= bounds.Width - edge)
        {
             dock = Avalonia.Controls.Dock.Right;
        }

        SetAttachedString(state.Item, "DockPanel.Dock", dock.ToString());
        RefreshSelectionAdorners();
    }

    private void UpdateReorderDrag(DragState state, Point currentPoint)
    {
        if (_currentVm is null)
        {
            return;
        }

        Point rootPoint = TranslateToRoot(currentPoint);
        Control? hit = ControlFactory.HitTest(_rootControl!, rootPoint);
        if (hit is null || !_currentVm.ControlMap.TryGetValue(hit, out DesignItem? target) || target is null)
        {
            _adornerLayer?.UpdateDropTarget(null, null);
            state.DropTarget = null;
            state.DropPosition = null;
            return;
        }

        if (ReferenceEquals(target, state.Item))
        {
            _adornerLayer?.UpdateDropTarget(null, null);
            state.DropTarget = null;
            state.DropPosition = null;
            return;
        }

        DropPosition position = ComputeDropPosition(target, currentPoint);
        state.DropTarget = target;
        state.DropPosition = position;

        Rect bounds = target.GetBoundsRelativeTo(GetSurfaceRoot());
        _adornerLayer?.UpdateDropTarget(bounds, position);
    }

    private void UpdateResize(DragState state, Point currentPoint)
    {
        if (_currentVm is null || state.ResizeDirection is null)
        {
            return;
        }

        Vector delta = currentPoint - state.StartPoint;
        Rect bounds = state.StartBounds;
        double left = bounds.Left;
        double top = bounds.Top;
        double width = bounds.Width;
        double height = bounds.Height;

        switch (state.ResizeDirection.Value)
        {
            case ResizeDirection.Left:
                left += delta.X;
                width -= delta.X;
                break;
            case ResizeDirection.Right:
                width += delta.X;
                break;
            case ResizeDirection.Top:
                top += delta.Y;
                height -= delta.Y;
                break;
            case ResizeDirection.Bottom:
                height += delta.Y;
                break;
            case ResizeDirection.TopLeft:
                left += delta.X;
                width -= delta.X;
                top += delta.Y;
                height -= delta.Y;
                break;
            case ResizeDirection.TopRight:
                width += delta.X;
                top += delta.Y;
                height -= delta.Y;
                break;
            case ResizeDirection.BottomLeft:
                left += delta.X;
                width -= delta.X;
                height += delta.Y;
                break;
            case ResizeDirection.BottomRight:
                width += delta.X;
                height += delta.Y;
                break;
        }

        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (_currentVm.SnapToGrid)
        {
            width = Snap(width, _currentVm.GridSnapSize);
            height = Snap(height, _currentVm.GridSnapSize);
        }

        if (_currentVm.SnapToGuides)
        {
            (left, top, width, height) = ApplyGuideSnapsToResize(state, left, top, width, height);
        }

        SetPropertyDouble(state.Item, "Width", width);
        SetPropertyDouble(state.Item, "Height", height);

        if (state.Item.VisualElement?.Parent is Canvas)
        {
            SetAttachedDouble(state.Item, "Canvas.Left", left);
            SetAttachedDouble(state.Item, "Canvas.Top", top);
        }

        UpdateSpacingGuidesForDrag(new Rect(left, top, width, height));
        RefreshSelectionAdorners();
    }

    private DropPosition ComputeDropPosition(DesignItem target, Point surfacePoint)
    {
        Rect bounds = target.GetBoundsRelativeTo(GetSurfaceRoot());
        double inset = Math.Min(bounds.Width, bounds.Height) * 0.25;

        if (surfacePoint.Y <= bounds.Top + inset || surfacePoint.X <= bounds.Left + inset)
        {
            return DropPosition.Before;
        }

        if (surfacePoint.Y >= bounds.Bottom - inset || surfacePoint.X >= bounds.Right - inset)
        {
            return DropPosition.After;
        }

        return DropPosition.Inside;
    }

    private DragMode GetDragMode(DesignItem item)
    {
        Control? parent = item.VisualElement?.Parent as Control;
        return parent switch
        {
            Canvas => DragMode.Canvas,
            Grid => DragMode.Grid,
            DockPanel => DragMode.Dock,
            StackPanel => DragMode.Reorder,
            WrapPanel => DragMode.Reorder,
            UniformGrid => DragMode.Reorder,
            Panel => DragMode.Reorder,
            _ => DragMode.Reorder
        };
    }

    private static int FindGridIndex(IReadOnlyList<RowDefinition> definitions, double position, double totalSize)
    {
        if (definitions.Count == 0)
        {
            return 0;
        }

        double offset = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            double size = definitions[i].ActualHeight;
            if (size <= 0)
            {
                size = totalSize / definitions.Count;
            }

            if (position <= offset + size)
            {
                return i;
            }

            offset += size;
        }

        return definitions.Count - 1;
    }

    private static int FindGridIndex(IReadOnlyList<ColumnDefinition> definitions, double position, double totalSize)
    {
        if (definitions.Count == 0)
        {
            return 0;
        }

        double offset = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            double size = definitions[i].ActualWidth;
            if (size <= 0)
            {
                size = totalSize / definitions.Count;
            }

            if (position <= offset + size)
            {
                return i;
            }

            offset += size;
        }

        return definitions.Count - 1;
    }

    private void SetPreviewHitTest(Control root, bool interactive)
    {
        root.IsHitTestVisible = interactive;

        switch (root)
        {
            case Panel panel:
                foreach (Control child in panel.Children.OfType<Control>())
                {
                    SetPreviewHitTest(child, interactive);
                }
                break;
            case Decorator decorator when decorator.Child is Control child:
                SetPreviewHitTest(child, interactive);
                break;
            case ContentControl contentControl when contentControl.Content is Control contentChild:
                SetPreviewHitTest(contentChild, interactive);
                break;
            case ItemsControl itemsControl when itemsControl.ItemsSource is IEnumerable items:
                foreach (object? item in items)
                {
                    if (item is Control itemControl)
                    {
                        SetPreviewHitTest(itemControl, interactive);
                    }
                }
                break;
        }
    }

    private static double Snap(double value, double gridSize)
    {
        if (gridSize <= 0)
        {
            return value;
        }

        return Math.Round(value / gridSize) * gridSize;
    }

    private (double left, double top, IReadOnlyList<double> snapHorizontal, IReadOnlyList<double> snapVertical)
        ApplySnapLines(DragState state, double left, double top, bool applySnap)
    {
        if (_currentVm is null || _canvas is null)
        {
            return (left, top, Array.Empty<double>(), Array.Empty<double>());
        }

        const double threshold = 6.0;
        double width = state.StartBounds.Width;
        double height = state.StartBounds.Height;

        List<double> candidateX = new();
        List<double> candidateY = new();

        foreach (DesignItem item in _currentVm.ItemMap.Values)
        {
            if (ReferenceEquals(item, state.Item))
            {
                continue;
            }

            if (state.StartPositions.ContainsKey(item.AstNodeId))
            {
                continue;
            }

            Rect bounds = item.GetBoundsRelativeTo(GetSurfaceRoot());
            candidateX.Add(bounds.Left);
            candidateX.Add(bounds.Center.X);
            candidateX.Add(bounds.Right);
            candidateY.Add(bounds.Top);
            candidateY.Add(bounds.Center.Y);
            candidateY.Add(bounds.Bottom);
        }

        double snappedLeft = left;
        double snappedTop = top;
        List<double> snapVertical = new();
        List<double> snapHorizontal = new();

        if (candidateX.Count > 0)
        {
            (double offset, double line) = FindSnapOffset(
                new[] { left, left + width / 2, left + width },
                candidateX,
                threshold);
            if (Math.Abs(offset) > 0)
            {
                if (applySnap)
                {
                    snappedLeft = left + offset;
                }
                snapVertical.Add(line);
            }
        }

        if (candidateY.Count > 0)
        {
            (double offset, double line) = FindSnapOffset(
                new[] { top, top + height / 2, top + height },
                candidateY,
                threshold);
            if (Math.Abs(offset) > 0)
            {
                if (applySnap)
                {
                    snappedTop = top + offset;
                }
                snapHorizontal.Add(line);
            }
        }

        return (snappedLeft, snappedTop, snapHorizontal, snapVertical);
    }

    private (double left, double top) ApplyGuideSnaps(DragState state, double left, double top, bool applySnap)
    {
        if (_currentVm is null)
        {
            return (left, top);
        }

        const double threshold = 6.0;
        double width = state.StartBounds.Width;
        double height = state.StartBounds.Height;

        if (_currentVm.VerticalGuides.Count > 0)
        {
            (double offset, _) = FindSnapOffset(
                new[] { left, left + width / 2, left + width },
                _currentVm.VerticalGuides,
                threshold);
            if (applySnap && Math.Abs(offset) > 0)
            {
                left += offset;
            }
        }

        if (_currentVm.HorizontalGuides.Count > 0)
        {
            (double offset, _) = FindSnapOffset(
                new[] { top, top + height / 2, top + height },
                _currentVm.HorizontalGuides,
                threshold);
            if (applySnap && Math.Abs(offset) > 0)
            {
                top += offset;
            }
        }

        return (left, top);
    }

    private (double left, double top, double width, double height) ApplyGuideSnapsToResize(
        DragState state,
        double left,
        double top,
        double width,
        double height)
    {
        if (_currentVm is null || state.ResizeDirection is null)
        {
            return (left, top, width, height);
        }

        const double threshold = 6.0;
        ResizeDirection direction = state.ResizeDirection.Value;

        if (_currentVm.VerticalGuides.Count > 0)
        {
            if (direction is ResizeDirection.Left or ResizeDirection.TopLeft or ResizeDirection.BottomLeft)
            {
                (double offset, _) = FindSnapOffset(new[] { left }, _currentVm.VerticalGuides, threshold);
                if (Math.Abs(offset) > 0)
                {
                    left += offset;
                    width -= offset;
                }
            }
            else if (direction is ResizeDirection.Right or ResizeDirection.TopRight or ResizeDirection.BottomRight)
            {
                double right = left + width;
                (double offset, _) = FindSnapOffset(new[] { right }, _currentVm.VerticalGuides, threshold);
                if (Math.Abs(offset) > 0)
                {
                    width += offset;
                }
            }
        }

        if (_currentVm.HorizontalGuides.Count > 0)
        {
            if (direction is ResizeDirection.Top or ResizeDirection.TopLeft or ResizeDirection.TopRight)
            {
                (double offset, _) = FindSnapOffset(new[] { top }, _currentVm.HorizontalGuides, threshold);
                if (Math.Abs(offset) > 0)
                {
                    top += offset;
                    height -= offset;
                }
            }
            else if (direction is ResizeDirection.Bottom or ResizeDirection.BottomLeft or ResizeDirection.BottomRight)
            {
                double bottom = top + height;
                (double offset, _) = FindSnapOffset(new[] { bottom }, _currentVm.HorizontalGuides, threshold);
                if (Math.Abs(offset) > 0)
                {
                    height += offset;
                }
            }
        }

        width = Math.Max(1, width);
        height = Math.Max(1, height);

        return (left, top, width, height);
    }

    private void UpdateSpacingGuidesForSelection()
    {
        if (_currentVm is null || _adornerLayer is null)
        {
            return;
        }

        if (!_currentVm.ShowSpacingGuides)
        {
            _adornerLayer.UpdateSpacingGuides(Array.Empty<SpacingGuide>());
            return;
        }

        Rect? selectionBounds = GetSelectionBounds();
        if (selectionBounds is null)
        {
            _adornerLayer.UpdateSpacingGuides(Array.Empty<SpacingGuide>());
            return;
        }

        _adornerLayer.UpdateSpacingGuides(BuildSpacingGuides(selectionBounds.Value));
    }

    private void UpdateSpacingGuidesForDrag(Rect selectionBounds)
    {
        if (_currentVm is null || _adornerLayer is null)
        {
            return;
        }

        if (!_currentVm.ShowSpacingGuides)
        {
            _adornerLayer.UpdateSpacingGuides(Array.Empty<SpacingGuide>());
            return;
        }

        _adornerLayer.UpdateSpacingGuides(BuildSpacingGuides(selectionBounds));
    }

    private IReadOnlyList<SpacingGuide> BuildSpacingGuides(Rect selectionBounds)
    {
        if (_currentVm is null || _canvas is null)
        {
            return Array.Empty<SpacingGuide>();
        }

        double? bestLeft = null;
        double? bestRight = null;
        double? bestTop = null;
        double? bestBottom = null;
        Rect leftBounds = default;
        Rect rightBounds = default;
        Rect topBounds = default;
        Rect bottomBounds = default;

        foreach (DesignItem item in _currentVm.ItemMap.Values)
        {
            if (_currentVm.Selection.SelectedItems.Contains(item))
            {
                continue;
            }

            Rect bounds = item.GetBoundsRelativeTo(GetSurfaceRoot());

            if (TryGetVerticalOverlap(selectionBounds, bounds, out double overlapY))
            {
                if (bounds.Right <= selectionBounds.Left)
                {
                    double distance = selectionBounds.Left - bounds.Right;
                    if (!bestLeft.HasValue || distance < bestLeft.Value)
                    {
                        bestLeft = distance;
                        leftBounds = bounds;
                    }
                }

                if (bounds.Left >= selectionBounds.Right)
                {
                    double distance = bounds.Left - selectionBounds.Right;
                    if (!bestRight.HasValue || distance < bestRight.Value)
                    {
                        bestRight = distance;
                        rightBounds = bounds;
                    }
                }
            }

            if (TryGetHorizontalOverlap(selectionBounds, bounds, out double overlapX))
            {
                if (bounds.Bottom <= selectionBounds.Top)
                {
                    double distance = selectionBounds.Top - bounds.Bottom;
                    if (!bestTop.HasValue || distance < bestTop.Value)
                    {
                        bestTop = distance;
                        topBounds = bounds;
                    }
                }

                if (bounds.Top >= selectionBounds.Bottom)
                {
                    double distance = bounds.Top - selectionBounds.Bottom;
                    if (!bestBottom.HasValue || distance < bestBottom.Value)
                    {
                        bestBottom = distance;
                        bottomBounds = bounds;
                    }
                }
            }
        }

        List<SpacingGuide> guides = new();

        if (bestLeft.HasValue)
        {
            double y = GetOverlapCenterY(selectionBounds, leftBounds);
            guides.Add(new SpacingGuide(
                new Point(leftBounds.Right, y),
                new Point(selectionBounds.Left, y),
                bestLeft.Value.ToString("0")));
        }

        if (bestRight.HasValue)
        {
            double y = GetOverlapCenterY(selectionBounds, rightBounds);
            guides.Add(new SpacingGuide(
                new Point(selectionBounds.Right, y),
                new Point(rightBounds.Left, y),
                bestRight.Value.ToString("0")));
        }

        if (bestTop.HasValue)
        {
            double x = GetOverlapCenterX(selectionBounds, topBounds);
            guides.Add(new SpacingGuide(
                new Point(x, topBounds.Bottom),
                new Point(x, selectionBounds.Top),
                bestTop.Value.ToString("0")));
        }

        if (bestBottom.HasValue)
        {
            double x = GetOverlapCenterX(selectionBounds, bottomBounds);
            guides.Add(new SpacingGuide(
                new Point(x, selectionBounds.Bottom),
                new Point(x, bottomBounds.Top),
                bestBottom.Value.ToString("0")));
        }

        return guides;
    }

    private static bool TryGetVerticalOverlap(Rect a, Rect b, out double center)
    {
        double top = Math.Max(a.Top, b.Top);
        double bottom = Math.Min(a.Bottom, b.Bottom);
        if (bottom <= top)
        {
            center = 0;
            return false;
        }

        center = (top + bottom) / 2;
        return true;
    }

    private static bool TryGetHorizontalOverlap(Rect a, Rect b, out double center)
    {
        double left = Math.Max(a.Left, b.Left);
        double right = Math.Min(a.Right, b.Right);
        if (right <= left)
        {
            center = 0;
            return false;
        }

        center = (left + right) / 2;
        return true;
    }

    private static double GetOverlapCenterY(Rect a, Rect b)
    {
        TryGetVerticalOverlap(a, b, out double center);
        return center;
    }

    private static double GetOverlapCenterX(Rect a, Rect b)
    {
        TryGetHorizontalOverlap(a, b, out double center);
        return center;
    }

    private static (double offset, double line) FindSnapOffset(
        IReadOnlyList<double> primaryLines,
        IReadOnlyList<double> candidateLines,
        double threshold)
    {
        double bestDelta = 0;
        double bestLine = 0;
        double bestDistance = threshold + 1;

        foreach (double primary in primaryLines)
        {
            foreach (double candidate in candidateLines)
            {
                double delta = candidate - primary;
                double distance = Math.Abs(delta);
                if (distance <= threshold && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDelta = delta;
                    bestLine = candidate;
                }
            }
        }

        return bestDistance <= threshold ? (bestDelta, bestLine) : (0, 0);
    }

    private static double GetAttachedDouble(DesignItem item, string propertyName)
    {
        string? raw = item.AstNode.GetPropertyValue(propertyName);
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }

        if (item.VisualElement is Control control)
        {
            return propertyName switch
            {
                "Canvas.Left" => double.IsNaN(Canvas.GetLeft(control)) ? 0 : Canvas.GetLeft(control),
                "Canvas.Top" => double.IsNaN(Canvas.GetTop(control)) ? 0 : Canvas.GetTop(control),
                _ => 0
            };
        }

        return 0;
    }

    private static int GetAttachedInt(DesignItem item, string propertyName)
    {
        string? raw = item.AstNode.GetPropertyValue(propertyName);
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }

        if (item.VisualElement is Control control)
        {
            return propertyName switch
            {
                "Grid.Row" => Grid.GetRow(control),
                "Grid.Column" => Grid.GetColumn(control),
                _ => 0
            };
        }

        return 0;
    }

    private static void SetAttachedDouble(DesignItem item, string propertyName, double value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        item.AstNode.SetPropertyValue(propertyName, text);

        if (item.VisualElement is Control control)
        {
            switch (propertyName)
            {
                case "Canvas.Left":
                    Canvas.SetLeft(control, value);
                    break;
                case "Canvas.Top":
                    Canvas.SetTop(control, value);
                    break;
            }
        }
    }

    private static void SetAttachedInt(DesignItem item, string propertyName, int value)
    {
        item.AstNode.SetPropertyValue(propertyName, value.ToString(CultureInfo.InvariantCulture));

        if (item.VisualElement is Control control)
        {
            switch (propertyName)
            {
                case "Grid.Row":
                    Grid.SetRow(control, value);
                    break;
                case "Grid.Column":
                    Grid.SetColumn(control, value);
                    break;
            }
        }
    }

    private static void SetAttachedString(DesignItem item, string propertyName, string value)
    {
        item.AstNode.SetPropertyValue(propertyName, value);

        if (item.VisualElement is Control control && propertyName == "DockPanel.Dock" && Enum.TryParse(value, out Avalonia.Controls.Dock dock))
        {
            DockPanel.SetDock(control, dock);
        }
    }

    private static void SetPropertyDouble(DesignItem item, string propertyName, double value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        item.AstNode.SetPropertyValue(propertyName, text);

        if (item.VisualElement is Control control)
        {
            switch (propertyName)
            {
                case "Width":
                    control.Width = value;
                    break;
                case "Height":
                    control.Height = value;
                    break;
            }
        }
    }

    private static double GetPropertyDouble(DesignItem item, string propertyName)
    {
        string? raw = item.AstNode.GetPropertyValue(propertyName);
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }

        if (item.VisualElement is Control control)
        {
            return propertyName switch
            {
                "Width" => double.IsNaN(control.Width) ? control.Bounds.Width : control.Width,
                "Height" => double.IsNaN(control.Height) ? control.Bounds.Height : control.Height,
                _ => 0
            };
        }

        return 0;
    }

    private static Thickness GetThickness(DesignItem item, string propertyName)
    {
        string? raw = item.AstNode.GetPropertyValue(propertyName);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            string[] parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double uniform))
            {
                return new Thickness(uniform);
            }
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double h) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                return new Thickness(h, v, h, v);
            }
            if (parts.Length == 4 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double l) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double t) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double r) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double b))
            {
                return new Thickness(l, t, r, b);
            }
        }

        if (item.VisualElement is Control control)
        {
            return control.Margin;
        }

        return default;
    }

    private static void SetThickness(DesignItem item, string propertyName, Thickness thickness)
    {
        string text = string.Join(",",
            thickness.Left.ToString(CultureInfo.InvariantCulture),
            thickness.Top.ToString(CultureInfo.InvariantCulture),
            thickness.Right.ToString(CultureInfo.InvariantCulture),
            thickness.Bottom.ToString(CultureInfo.InvariantCulture));

        item.AstNode.SetPropertyValue(propertyName, text);

        if (item.VisualElement is Control control && propertyName == "Margin")
        {
            control.Margin = thickness;
        }
    }

    private Point TranslateFromCanvas(Control target, Point canvasPoint)
    {
        if (_canvas is null)
        {
            return canvasPoint;
        }

        Point? origin = target.TranslatePoint(new Point(0, 0), _canvas);
        return origin.HasValue ? canvasPoint - origin.Value : canvasPoint;
    }

    private Point TranslateToRoot(Point canvasPoint)
    {
        if (_canvas is null || _rootControl is null)
        {
            return canvasPoint;
        }

        Point? origin = _rootControl.TranslatePoint(new Point(0, 0), _canvas);
        return origin.HasValue ? canvasPoint - origin.Value : canvasPoint;
    }

    private sealed class DragState
    {
        public required DesignItem Item { get; init; }
        public required Point StartPoint { get; init; }
        public required Rect StartBounds { get; init; }
        public required DragMode Mode { get; init; }
        public Point LastPoint { get; set; }
        public bool HasMoved { get; set; }
        public ResizeDirection? ResizeDirection { get; init; }
        public double StartCanvasLeft { get; init; }
        public double StartCanvasTop { get; init; }
        public DesignItem? DropTarget { get; set; }
        public DropPosition? DropPosition { get; set; }
        public Dictionary<Guid, Point> StartPositions { get; } = new();
    }

    private sealed class MarqueeState
    {
        public required Point StartPoint { get; init; }
        public required Point CurrentPoint { get; set; }
        public bool Additive { get; init; }
    }

    private enum DragMode
    {
        Canvas,
        Grid,
        Dock,
        Reorder,
        Resize
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

    // Applies the workspace application's themes and resource includes to the design
    // canvas so instantiated third-party controls (workspace assemblies) find their
    // control themes and DynamicResource brushes.
    private void ApplyWorkspaceThemes(Panel canvas)
    {
        if (_appliedWorkspaceThemeVersion == WorkspaceDesignThemeRegistry.Version)
        {
            return;
        }

        foreach (IStyle style in _workspaceThemeStyles)
        {
            canvas.Styles.Remove(style);
        }

        foreach (IResourceProvider provider in _workspaceThemeResources)
        {
            canvas.Resources.MergedDictionaries.Remove(provider);
        }

        _workspaceThemeStyles.Clear();
        _workspaceThemeResources.Clear();

        foreach (Func<IResourceProvider?> factory in WorkspaceDesignThemeRegistry.ResourceFactories)
        {
            try
            {
                if (factory() is { } provider)
                {
                    canvas.Resources.MergedDictionaries.Add(provider);
                    _workspaceThemeResources.Add(provider);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Workspace design resource include failed");
            }
        }

        foreach (Func<IStyle?> factory in WorkspaceDesignThemeRegistry.StyleFactories)
        {
            try
            {
                if (factory() is { } style)
                {
                    canvas.Styles.Add(style);
                    _workspaceThemeStyles.Add(style);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Workspace design style failed");
            }
        }

        _appliedWorkspaceThemeVersion = WorkspaceDesignThemeRegistry.Version;
    }

    // Replacement for Design.ApplyDesignModeProperties, removed in Avalonia 12:
    // applies Design.Width/Height/DataContext/DesignStyle to the instantiated tree.
    private static void ApplyDesignModeProperties(Control control)
    {
        if (control.IsSet(Design.WidthProperty))
        {
            control.Width = control.GetValue(Design.WidthProperty);
        }

        if (control.IsSet(Design.HeightProperty))
        {
            control.Height = control.GetValue(Design.HeightProperty);
        }

        if (control.IsSet(Design.DataContextProperty))
        {
            control.DataContext = control.GetValue(Design.DataContextProperty);
        }

        if (control.GetValue(Design.DesignStyleProperty) is { } designStyle)
        {
            control.Styles.Add(designStyle);
        }
    }

    private DesignerDocumentViewModel? FindDocumentViewModel()
    {
        if (DataContext is DesignerDocumentViewModel selfVm)
        {
            return selfVm;
        }

        if (DataContext is DesignerDocument selfDoc)
        {
            return selfDoc.DocumentViewModel;
        }

        foreach (var ancestor in this.GetVisualAncestors())
        {
            if (ancestor is Control control)
            {
                if (control.DataContext is DesignerDocumentViewModel dvm)
                {
                    return dvm;
                }

                if (control.DataContext is DesignerDocument dockDoc)
                {
                    return dockDoc.DocumentViewModel;
                }
            }
        }

        return null;
    }
}
