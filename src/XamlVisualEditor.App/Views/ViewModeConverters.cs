using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Static converters for <see cref="DocumentViewMode"/> to boolean/visibility checks.
/// Used by <see cref="DesignerDocumentView"/> radio buttons and panel visibility.
/// </summary>
public static class ViewModeConverters
{
    /// <summary>True when ViewMode is Split.</summary>
    public static readonly IValueConverter IsSplit =
        new ViewModeEqualityConverter(DocumentViewMode.Split);

    /// <summary>True when ViewMode is Design.</summary>
    public static readonly IValueConverter IsDesign =
        new ViewModeEqualityConverter(DocumentViewMode.Design);

    /// <summary>True when ViewMode is Code.</summary>
    public static readonly IValueConverter IsCode =
        new ViewModeEqualityConverter(DocumentViewMode.Code);

    /// <summary>True when the designer surface should be visible (Design or Split).</summary>
    public static readonly IValueConverter ShowDesigner =
        new ViewModeSetConverter(DocumentViewMode.Design, DocumentViewMode.Split);

    /// <summary>True when the code editor should be visible (Code or Split).</summary>
    public static readonly IValueConverter ShowCode =
        new ViewModeSetConverter(DocumentViewMode.Code, DocumentViewMode.Split);

    /// <summary>Row height for the designer panel.</summary>
    public static readonly IValueConverter DesignerRowHeight =
        new ViewModeRowHeightConverter(
            design: new GridLength(1, GridUnitType.Star),
            split: new GridLength(1, GridUnitType.Star),
            code: new GridLength(0, GridUnitType.Pixel));

    /// <summary>Row height for the splitter between panels.</summary>
    public static readonly IValueConverter SplitterRowHeight =
        new ViewModeRowHeightConverter(
            design: new GridLength(0, GridUnitType.Pixel),
            split: GridLength.Auto,
            code: new GridLength(0, GridUnitType.Pixel));

    /// <summary>Row height for the code editor panel.</summary>
    public static readonly IValueConverter CodeRowHeight =
        new ViewModeRowHeightConverter(
            design: new GridLength(0, GridUnitType.Pixel),
            split: new GridLength(1, GridUnitType.Star),
            code: new GridLength(1, GridUnitType.Star));

    private sealed class ViewModeEqualityConverter : IValueConverter
    {
        private readonly DocumentViewMode _target;

        public ViewModeEqualityConverter(DocumentViewMode target) => _target = target;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is DocumentViewMode mode && mode == _target;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? _target : Avalonia.Data.BindingOperations.DoNothing;
    }

    private sealed class ViewModeSetConverter : IValueConverter
    {
        private readonly DocumentViewMode _a;
        private readonly DocumentViewMode _b;

        public ViewModeSetConverter(DocumentViewMode a, DocumentViewMode b)
        {
            _a = a;
            _b = b;
        }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is DocumentViewMode mode && (mode == _a || mode == _b);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Avalonia.Data.BindingOperations.DoNothing;
    }

    private sealed class ViewModeRowHeightConverter : IValueConverter
    {
        private readonly GridLength _design;
        private readonly GridLength _split;
        private readonly GridLength _code;

        public ViewModeRowHeightConverter(GridLength design, GridLength split, GridLength code)
        {
            _design = design;
            _split = split;
            _code = code;
        }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is DocumentViewMode mode
                ? mode switch
                {
                    DocumentViewMode.Design => _design,
                    DocumentViewMode.Split => _split,
                    DocumentViewMode.Code => _code,
                    _ => _split
                }
                : _split;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Avalonia.Data.BindingOperations.DoNothing;
    }
}
