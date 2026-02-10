using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Serilog;

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

        Log.Logger.Information("Dropped files ({Count}): {Paths}", paths.Count, string.Join(", ", paths));

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
        Log.Logger.Information("Drop data: HasFile={HasFile}, HasText={HasText}",
            data.Contains(DataFormat.File),
            data.Contains(DataFormat.Text));

        IStorageItem[]? storageItems = data.TryGetFiles();
        if (storageItems is { Length: > 0 })
        {
            Log.Logger.Information("Drop data TryGetFiles count: {Count}", storageItems.Length);
            foreach (IStorageItem storageItem in storageItems)
            {
                candidatePaths.Add(storageItem.Path.LocalPath);
            }
        }
        if (data.Contains(DataFormat.File))
        {
            object? value = data.TryGetValue(DataFormat.File);
            Log.Logger.Information("Drop data File type: {Type}",
                value is null ? "<null>" : value.GetType().FullName);
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
            else if (value is System.Collections.IEnumerable rawItems)
            {
                foreach (object? entry in rawItems)
                {
                    if (entry is IStorageItem storageItem)
                    {
                        candidatePaths.Add(storageItem.Path.LocalPath);
                    }
                    else if (entry is string path)
                    {
                        candidatePaths.Add(path);
                    }
                }
            }
        }

        if (data.Contains(DataFormat.Text))
        {
            object? textValue = data.TryGetValue(DataFormat.Text);
            Log.Logger.Information("Drop data Text type: {Type}",
                textValue is null ? "<null>" : textValue.GetType().FullName);
            if (textValue is string text)
            {
                AddTextPaths(text, candidatePaths);
            }
            else if (textValue is IEnumerable<string> textPaths)
            {
                foreach (string textPath in textPaths)
                {
                    AddTextPaths(textPath, candidatePaths);
                }
            }
        }

        Log.Logger.Information("Drop data candidate paths: {Count}", candidatePaths.Count);

        paths = candidatePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Log.Logger.Information("Drop data distinct paths: {Count}", paths.Count);
        return paths.Count > 0;
    }

    private static void AddTextPaths(string text, List<string> candidatePaths)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        bool added = false;
        if (text.Contains("file://", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Match match in Regex.Matches(text, "file://[^\\s]+", RegexOptions.IgnoreCase))
            {
                if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri))
                {
                    candidatePaths.Add(uri.LocalPath);
                    added = true;
                }
            }
        }

        if (added)
        {
            return;
        }

        string[] parts = text.Split(new[] { '\n', '\r', '\0', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

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
