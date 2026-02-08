using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public sealed class CanvasFileDropBehavior
{
    public static readonly AttachedProperty<ICommand?> DropCommandProperty =
        AvaloniaProperty.RegisterAttached<CanvasFileDropBehavior, Control, ICommand?>("DropCommand");

    static CanvasFileDropBehavior()
    {
        DropCommandProperty.Changed.AddClassHandler<Control>(OnDropCommandChanged);
    }

    public static ICommand? GetDropCommand(Control control)
    {
        return control.GetValue(DropCommandProperty);
    }

    public static void SetDropCommand(Control control, ICommand? value)
    {
        control.SetValue(DropCommandProperty, value);
    }

    private static void OnDropCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is ICommand && e.OldValue is null)
        {
            DragDrop.SetAllowDrop(control, true);
            control.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        }
        else if (e.NewValue is null && e.OldValue is ICommand)
        {
            control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            control.RemoveHandler(DragDrop.DropEvent, OnDrop);
        }
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        if (TryGetFilePaths(e, out _))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        ICommand? command = GetDropCommand(control);
        if (command is null)
        {
            return;
        }

        if (!TryGetFilePaths(e, out List<string>? paths) || paths is null || paths.Count == 0)
        {
            return;
        }

        var position = e.GetPosition(control);
        CanvasDropInfo payload = new(position.X, position.Y, paths);

        if (command.CanExecute(payload))
        {
            command.Execute(payload);
            e.Handled = true;
        }
    }

    private static bool TryGetFilePaths(DragEventArgs e, out List<string>? paths)
    {
        paths = null;
        IDataTransfer data = e.DataTransfer;
        List<string> candidatePaths = new();

        if (data.Contains(DataFormat.File))
        {
            object? value = data.TryGetValue(DataFormat.File);
            if (value is IEnumerable<string> pathList)
            {
                candidatePaths.AddRange(pathList);
            }
            else if (value is IStorageItem item)
            {
                candidatePaths.Add(item.Path.LocalPath);
            }
            else if (value is IEnumerable<IStorageItem> items)
            {
                foreach (IStorageItem storageItem in items)
                {
                    candidatePaths.Add(storageItem.Path.LocalPath);
                }
            }
        }

        if (data.Contains(DataFormat.Text))
        {
            object? textValue = data.TryGetValue(DataFormat.Text);
            if (textValue is string text)
            {
                string[] parts = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
                        {
                            candidatePaths.Add(uri.LocalPath);
                            continue;
                        }
                    }

                    candidatePaths.Add(trimmed);
                }
            }
        }

        if (candidatePaths.Count == 0)
        {
            return false;
        }

        paths = candidatePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return paths.Count > 0;
    }
}
