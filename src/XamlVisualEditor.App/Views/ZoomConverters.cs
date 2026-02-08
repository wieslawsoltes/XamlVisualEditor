using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace XamlVisualEditor.App.Views;

public static class ZoomConverters
{
    public static readonly IMultiValueConverter Scale = new ScaleConverter();

    private sealed class ScaleConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
            {
                return 0.0;
            }

            if (values[0] is double size && values[1] is double zoom)
            {
                return size * zoom;
            }

            return 0.0;
        }
    }
}
