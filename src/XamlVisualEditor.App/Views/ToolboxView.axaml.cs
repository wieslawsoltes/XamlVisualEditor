using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using XamlVisualEditor.Designer.DragDrop;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the toolbox view.
/// Initiates drag-and-drop operations when the user drags a toolbox item
/// onto the design surface.
/// </summary>
public sealed partial class ToolboxView : UserControl
{
    private ToolboxItemViewModel? _dragCandidate;
    private Point _dragStartPoint;
    private bool _isDragging;
    private ListBox? _listBox;

    public ToolboxView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        _listBox = this.FindControl<ListBox>("ToolboxList");
        if (_listBox is null)
        {
            return;
        }

        _listBox.AddHandler(PointerPressedEvent, OnPointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
        _listBox.AddHandler(PointerMovedEvent, OnPointerMoved,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
        _listBox.AddHandler(PointerReleasedEvent, OnPointerReleased,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_listBox is null)
        {
            return;
        }

        if (!e.GetCurrentPoint(_listBox).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ToolboxItemViewModel? item = GetToolboxItemFromSource(e.Source);
        if (item is null)
        {
            return;
        }

        _dragCandidate = item;
        _dragStartPoint = e.GetPosition(_listBox);
        e.Pointer.Capture(_listBox);
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_listBox is null || _dragCandidate is null)
        {
            return;
        }

        if (!_isDragging && !e.GetCurrentPoint(_listBox).Properties.IsLeftButtonPressed)
        {
            _dragCandidate = null;
            return;
        }

        if (_isDragging)
        {
            return;
        }

        Point position = e.GetPosition(_listBox);
        if (Math.Abs(position.X - _dragStartPoint.X) < 4 && Math.Abs(position.Y - _dragStartPoint.Y) < 4)
        {
            return;
        }

        _isDragging = true;

        DataTransfer data = new();
        data.Add(DataTransferItem.Create(DesignerDataFormats.ToolboxItem, _dragCandidate.TypeName));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);

        e.Pointer.Capture(null);

        _isDragging = false;
        _dragCandidate = null;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragCandidate = null;
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private static ToolboxItemViewModel? GetToolboxItemFromSource(object? source)
    {
        if (source is not Visual visual)
        {
            return null;
        }

        ListBoxItem? container = visual.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        return container?.DataContext as ToolboxItemViewModel;
    }
}
