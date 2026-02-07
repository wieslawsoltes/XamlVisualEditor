using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace XamlVisualEditor.PropertyEditor;

/// <summary>
/// Extracts a single field (Left, Top, Right, Bottom) from a Thickness string "L,T,R,B" or "All".
/// </summary>
public sealed class ThicknessFieldConverter : IValueConverter
{
    /// <summary>0=Left, 1=Top, 2=Right, 3=Bottom.</summary>
    public int FieldIndex { get; set; }

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
        {
            return "0";
        }

        string[] parts = s.Split(',');
        return parts.Length switch
        {
            1 => parts[0].Trim(),
            2 => FieldIndex is 0 or 2 ? parts[0].Trim() : parts[1].Trim(),
            4 when FieldIndex < 4 => parts[FieldIndex].Trim(),
            _ => "0"
        };
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ConvertBack is handled by the ViewModel; not used here
        return value;
    }
}

/// <summary>
/// Converts a boolean? (nullable) to string for CheckBox three-state support.
/// </summary>
public sealed class BoolStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s)
        {
            return null;
        }

        return s.Equals("True", StringComparison.OrdinalIgnoreCase) ? true
            : s.Equals("False", StringComparison.OrdinalIgnoreCase) ? false
            : null;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => "True",
            false => "False",
            _ => null
        };
    }
}
