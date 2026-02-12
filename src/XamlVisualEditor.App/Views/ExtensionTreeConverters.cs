using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Data.Converters;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public static class ExtensionTreeConverters
{
    public static readonly IValueConverter OpenCommand =
        new NodeCommandConverter(node => node.OpenCommand);
    public static readonly IValueConverter OpenWorkspaceCommand =
        new NodeCommandConverter(node => node.OpenWorkspaceCommand);
    public static readonly IValueConverter NewFileCommand =
        new NodeCommandConverter(node => node.NewFileCommand);
    public static readonly IValueConverter NewFolderCommand =
        new NodeCommandConverter(node => node.NewFolderCommand);
    public static readonly IValueConverter RenameCommand =
        new NodeCommandConverter(node => node.RenameCommand);
    public static readonly IValueConverter DeleteCommand =
        new NodeCommandConverter(node => node.DeleteCommand);

    public static readonly IValueConverter CanOpen =
        new NodeBoolConverter(node => node.CanOpen);
    public static readonly IValueConverter CanOpenWorkspace =
        new NodeBoolConverter(node => node.CanOpenWorkspace);
    public static readonly IValueConverter CanCreateFile =
        new NodeBoolConverter(node => node.CanCreateFile);
    public static readonly IValueConverter CanCreateFolder =
        new NodeBoolConverter(node => node.CanCreateFolder);
    public static readonly IValueConverter CanRename =
        new NodeBoolConverter(node => node.CanRename);
    public static readonly IValueConverter CanDelete =
        new NodeBoolConverter(node => node.CanDelete);

    private sealed class NodeBoolConverter : IValueConverter
    {
        private readonly Func<ExtensionTreeNodeViewModel, bool> _selector;

        public NodeBoolConverter(Func<ExtensionTreeNodeViewModel, bool> selector)
        {
            _selector = selector;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is ExtensionTreeNodeViewModel node && _selector(node);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NodeCommandConverter : IValueConverter
    {
        private readonly Func<ExtensionTreeNodeViewModel, ICommand?> _selector;

        public NodeCommandConverter(Func<ExtensionTreeNodeViewModel, ICommand?> selector)
        {
            _selector = selector;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is ExtensionTreeNodeViewModel node ? _selector(node) : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
