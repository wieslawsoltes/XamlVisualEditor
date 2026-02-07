using System;
using System.Globalization;
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
}
