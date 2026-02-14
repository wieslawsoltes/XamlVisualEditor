using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace XamlVisualEditor.PropertyEditorExtension;

public sealed class BooleanInverseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return !flag;
        }

        return value is null && targetType == typeof(bool) ? false : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return !flag;
        }

        return value;
    }
}
