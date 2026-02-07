using System;
using Avalonia.Data.Converters;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public static class SolutionExplorerConverters
{
    public static readonly IValueConverter IsXamlFile = new KindEqualsConverter(SolutionExplorerNodeKind.XamlFile, true);
    public static readonly IValueConverter IsNotXamlFile = new KindEqualsConverter(SolutionExplorerNodeKind.XamlFile, false);

    private sealed class KindEqualsConverter : IValueConverter
    {
        private readonly SolutionExplorerNodeKind _kind;
        private readonly bool _isMatch;

        public KindEqualsConverter(SolutionExplorerNodeKind kind, bool isMatch)
        {
            _kind = kind;
            _isMatch = isMatch;
        }

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value is SolutionExplorerNodeKind kind && (kind == _kind) == _isMatch;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
