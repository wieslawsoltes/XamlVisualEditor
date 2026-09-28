using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using XamlVisualEditor.App.Services;

namespace XamlVisualEditor.App.Views;

/// <summary>Persists and restores the split ratios for a three-part designer grid.</summary>
public sealed class SplitterLayoutBehavior
{
    /// <summary>Identifies the attached property that selects the persisted splitter layout.</summary>
    public static readonly AttachedProperty<string?> PersistenceKeyProperty =
        AvaloniaProperty.RegisterAttached<SplitterLayoutBehavior, Grid, string?>("PersistenceKey");

    private static readonly AttachedProperty<bool> HandlersAttachedProperty =
        AvaloniaProperty.RegisterAttached<SplitterLayoutBehavior, Grid, bool>("HandlersAttached");

    static SplitterLayoutBehavior()
    {
        PersistenceKeyProperty.Changed.AddClassHandler<Grid>(OnPersistenceKeyChanged);
    }

    /// <summary>Gets the key for the persisted splitter layout.</summary>
    public static string? GetPersistenceKey(Grid grid)
    {
        return grid.GetValue(PersistenceKeyProperty);
    }

    /// <summary>Sets the key for the persisted splitter layout.</summary>
    public static void SetPersistenceKey(Grid grid, string? value)
    {
        grid.SetValue(PersistenceKeyProperty, value);
    }

    internal static double CalculateRatio(double first, double second)
    {
        double total = first + second;
        return total > 0 ? UiLayoutStateStore.NormalizeRatio(first / total) : 0.5;
    }

    private static void OnPersistenceKeyChanged(Grid grid, AvaloniaPropertyChangedEventArgs e)
    {
        bool enabled = !string.IsNullOrWhiteSpace(e.NewValue as string);
        bool attached = grid.GetValue(HandlersAttachedProperty);

        if (enabled && !attached)
        {
            grid.AttachedToVisualTree += OnAttachedToVisualTree;
            grid.DetachedFromVisualTree += OnDetachedFromVisualTree;
            grid.SetValue(HandlersAttachedProperty, true);
        }
        else if (!enabled && attached)
        {
            grid.AttachedToVisualTree -= OnAttachedToVisualTree;
            grid.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            DetachSplitters(grid);
            grid.SetValue(HandlersAttachedProperty, false);
        }
    }

    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Grid grid)
        {
            return;
        }

        ApplyState(grid);
        foreach (GridSplitter splitter in grid.Children.OfType<GridSplitter>())
        {
            splitter.DragCompleted -= OnDragCompleted;
            splitter.DragCompleted += OnDragCompleted;
        }
    }

    private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Grid grid)
        {
            DetachSplitters(grid);
        }
    }

    private static void OnDragCompleted(object? sender, VectorEventArgs e)
    {
        if (sender is not GridSplitter splitter || splitter.Parent is not Grid grid)
        {
            return;
        }

        string? key = GetPersistenceKey(grid);
        if (string.IsNullOrWhiteSpace(key) || grid.ColumnDefinitions.Count < 3 || grid.RowDefinitions.Count < 3)
        {
            return;
        }

        double columnRatio = CalculateRatio(
            grid.ColumnDefinitions[0].ActualWidth,
            grid.ColumnDefinitions[2].ActualWidth);
        double rowRatio = CalculateRatio(
            grid.RowDefinitions[0].ActualHeight,
            grid.RowDefinitions[2].ActualHeight);

        UiLayoutStateStore.Update(state =>
        {
            if (!state.Splitters.TryGetValue(key, out SplitterLayoutState? layout))
            {
                layout = new SplitterLayoutState();
                state.Splitters[key] = layout;
            }

            layout.ColumnRatio = columnRatio;
            layout.RowRatio = rowRatio;
        });
    }

    private static void ApplyState(Grid grid)
    {
        string? key = GetPersistenceKey(grid);
        if (string.IsNullOrWhiteSpace(key) || grid.ColumnDefinitions.Count < 3 || grid.RowDefinitions.Count < 3)
        {
            return;
        }

        UiLayoutState state = UiLayoutStateStore.Load();
        if (!state.Splitters.TryGetValue(key, out SplitterLayoutState? layout))
        {
            return;
        }

        grid.ColumnDefinitions[0].Width = new GridLength(layout.ColumnRatio, GridUnitType.Star);
        grid.ColumnDefinitions[2].Width = new GridLength(1 - layout.ColumnRatio, GridUnitType.Star);
        grid.RowDefinitions[0].Height = new GridLength(layout.RowRatio, GridUnitType.Star);
        grid.RowDefinitions[2].Height = new GridLength(1 - layout.RowRatio, GridUnitType.Star);
    }

    private static void DetachSplitters(Grid grid)
    {
        foreach (GridSplitter splitter in grid.Children.OfType<GridSplitter>())
        {
            splitter.DragCompleted -= OnDragCompleted;
        }
    }
}
