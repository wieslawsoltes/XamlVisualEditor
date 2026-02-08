using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace XamlVisualEditor.App.Views;

public sealed class FileDropBehavior
{
    public static readonly AttachedProperty<ICommand?> OpenFileCommandProperty =
        AvaloniaProperty.RegisterAttached<FileDropBehavior, Control, ICommand?>("OpenFileCommand");

    public static readonly AttachedProperty<ICommand?> OpenPathsCommandProperty =
        AvaloniaProperty.RegisterAttached<FileDropBehavior, Control, ICommand?>("OpenPathsCommand");

    private static readonly AttachedProperty<bool> HandlersAttachedProperty =
        AvaloniaProperty.RegisterAttached<FileDropBehavior, Control, bool>("HandlersAttached");

    static FileDropBehavior()
    {
        OpenFileCommandProperty.Changed.AddClassHandler<Control>(OnOpenFileCommandChanged);
        OpenPathsCommandProperty.Changed.AddClassHandler<Control>(OnOpenPathsCommandChanged);
    }

    public static ICommand? GetOpenFileCommand(Control control)
    {
        return control.GetValue(OpenFileCommandProperty);
    }

    public static void SetOpenFileCommand(Control control, ICommand? value)
    {
        control.SetValue(OpenFileCommandProperty, value);
    }

    public static ICommand? GetOpenPathsCommand(Control control)
    {
        return control.GetValue(OpenPathsCommandProperty);
    }

    public static void SetOpenPathsCommand(Control control, ICommand? value)
    {
        control.SetValue(OpenPathsCommandProperty, value);
    }

    private static void OnOpenFileCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        UpdateHandlers(control);
    }

    private static void OnOpenPathsCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        UpdateHandlers(control);
    }

    private static void UpdateHandlers(Control control)
    {
        bool hasCommand = GetOpenFileCommand(control) is not null || GetOpenPathsCommand(control) is not null;
        bool attached = control.GetValue(HandlersAttachedProperty);

        if (hasCommand && !attached)
        {
            DragDrop.SetAllowDrop(control, true);
            control.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.SetValue(HandlersAttachedProperty, true);
        }
        else if (!hasCommand && attached)
        {
            control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            control.RemoveHandler(DragDrop.DropEvent, OnDrop);
            control.SetValue(HandlersAttachedProperty, false);
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

        ICommand? command = GetOpenFileCommand(control);
        ICommand? pathsCommand = GetOpenPathsCommand(control);
        if (command is null && pathsCommand is null)
        {
            return;
        }

        if (!TryGetFilePaths(e, out List<string>? paths) || paths is null || paths.Count == 0)
        {
            return;
        }

        if (pathsCommand is not null && (paths.Count > 1 || command is null))
        {
            if (pathsCommand.CanExecute(paths))
            {
                pathsCommand.Execute(paths);
                e.Handled = true;
            }
            return;
        }

        string filePath = paths[0];
        if (command is not null && command.CanExecute(filePath))
        {
            command.Execute(filePath);
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

        paths = candidatePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return paths.Count > 0;
    }
}
