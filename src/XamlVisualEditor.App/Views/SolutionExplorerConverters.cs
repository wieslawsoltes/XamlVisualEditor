using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Data.Converters;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public static class SolutionExplorerConverters
{
    public static readonly IValueConverter IsOpenableFile = new KindMultiEqualsConverter(
        new[] { SolutionExplorerNodeKind.XamlFile, SolutionExplorerNodeKind.File }, true);
    public static readonly IValueConverter IsNotOpenableFile = new KindMultiEqualsConverter(
        new[] { SolutionExplorerNodeKind.XamlFile, SolutionExplorerNodeKind.File }, false);
    public static readonly IValueConverter IsProject = new KindMultiEqualsConverter(
        new[] { SolutionExplorerNodeKind.Project }, true);
    public static readonly IValueConverter OpenCommand = new OpenCommandConverter();

    private sealed class KindMultiEqualsConverter : IValueConverter
    {
        private readonly HashSet<SolutionExplorerNodeKind> _kinds;
        private readonly bool _isMatch;

        public KindMultiEqualsConverter(IEnumerable<SolutionExplorerNodeKind> kinds, bool isMatch)
        {
            _kinds = new HashSet<SolutionExplorerNodeKind>(kinds);
            _isMatch = isMatch;
        }

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is SolutionExplorerNodeViewModel node)
            {
                return _kinds.Contains(node.Kind) == _isMatch;
            }

            return value is SolutionExplorerNodeKind kind && _kinds.Contains(kind) == _isMatch;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class OpenCommandConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value is SolutionExplorerNodeViewModel node ? node.OpenCommand : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
