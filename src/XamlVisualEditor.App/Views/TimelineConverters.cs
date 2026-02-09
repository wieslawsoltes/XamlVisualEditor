using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XamlVisualEditor.App.Views;

public static class TimelineConverters
{
    public static readonly IMultiValueConverter TimeToPixels = new TimeToPixelsConverter();
    public static readonly IValueConverter SelectionToBrush = new SelectionToBrushConverter();
    public static readonly IValueConverter ValidityToBrush = new ValidityToBrushConverter();

    private sealed class TimeToPixelsConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
            {
                return 0.0;
            }

            if (values[0] is double time && values[1] is double pixelsPerSecond)
            {
                return Math.Max(0.0, time * pixelsPerSecond);
            }

            return 0.0;
        }
    }

    private sealed class SelectionToBrushConverter : IValueConverter
    {
        private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#F4C95D"));
        private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#FFDD7A"));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return SelectedBrush;
            }

            return DefaultBrush;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ValidityToBrushConverter : IValueConverter
    {
        private static readonly IBrush InvalidBrush = new SolidColorBrush(Color.Parse("#E24A4A"));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isValid && !isValid)
            {
                return InvalidBrush;
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}
