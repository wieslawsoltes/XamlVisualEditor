using System;
using System.Collections.Generic;
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

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xaml",
        ".axaml",
        ".sln",
        ".slnx",
        ".csproj"
    };

    static FileDropBehavior()
    {
        OpenFileCommandProperty.Changed.AddClassHandler<Control>(OnOpenFileCommandChanged);
    }

    public static ICommand? GetOpenFileCommand(Control control)
    {
        return control.GetValue(OpenFileCommandProperty);
    }

    public static void SetOpenFileCommand(Control control, ICommand? value)
    {
        control.SetValue(OpenFileCommandProperty, value);
    }

    private static void OnOpenFileCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
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
        if (TryGetSupportedFilePath(e, out _))
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
        if (command is null)
        {
            return;
        }

        if (!TryGetSupportedFilePath(e, out string? filePath))
        {
            return;
        }

        if (command.CanExecute(filePath))
        {
            command.Execute(filePath);
            e.Handled = true;
        }
    }

    private static bool TryGetSupportedFilePath(DragEventArgs e, out string? filePath)
    {
        filePath = null;
        IDataTransfer data = e.DataTransfer;
        if (!data.Contains(DataFormat.File))
        {
            return false;
        }

        IStorageItem? item = data.TryGetValue(DataFormat.File);
        if (item is not IStorageFile file)
        {
            return false;
        }

        string path = file.Path.LocalPath;
        if (IsSupportedPath(path))
        {
            filePath = path;
            return true;
        }

        return false;
    }

    private static bool IsSupportedPath(string path)
    {
        string extension = System.IO.Path.GetExtension(path);
        return SupportedExtensions.Contains(extension);
    }
}
