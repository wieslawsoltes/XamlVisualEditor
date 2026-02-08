using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
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

    /// <summary>True when SplitOrientation is Horizontal.</summary>
    public static readonly IValueConverter IsSplitHorizontal =
        new OrientationEqualityConverter(Orientation.Horizontal);

    /// <summary>True when SplitOrientation is Vertical.</summary>
    public static readonly IValueConverter IsSplitVertical =
        new OrientationEqualityConverter(Orientation.Vertical);

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

    /// <summary>Row length for the designer panel.</summary>
    public static readonly IMultiValueConverter DesignerRowLength =
        new SplitLengthConverter(SplitLengthRole.Design, SplitLengthAxis.Row);

    /// <summary>Row length for the splitter between panels.</summary>
    public static readonly IMultiValueConverter SplitterRowLength =
        new SplitLengthConverter(SplitLengthRole.Splitter, SplitLengthAxis.Row);

    /// <summary>Row length for the code editor panel.</summary>
    public static readonly IMultiValueConverter CodeRowLength =
        new SplitLengthConverter(SplitLengthRole.Code, SplitLengthAxis.Row);

    /// <summary>Column length for the designer panel.</summary>
    public static readonly IMultiValueConverter DesignerColumnLength =
        new SplitLengthConverter(SplitLengthRole.Design, SplitLengthAxis.Column);

    /// <summary>Column length for the splitter between panels.</summary>
    public static readonly IMultiValueConverter SplitterColumnLength =
        new SplitLengthConverter(SplitLengthRole.Splitter, SplitLengthAxis.Column);

    /// <summary>Column length for the code editor panel.</summary>
    public static readonly IMultiValueConverter CodeColumnLength =
        new SplitLengthConverter(SplitLengthRole.Code, SplitLengthAxis.Column);

    /// <summary>Row index for the designer panel.</summary>
    public static readonly IMultiValueConverter DesignerRowIndex =
        new SplitIndexConverter(SplitLengthRole.Design, SplitLengthAxis.Row);

    /// <summary>Row index for the splitter between panels.</summary>
    public static readonly IMultiValueConverter SplitterRowIndex =
        new SplitIndexConverter(SplitLengthRole.Splitter, SplitLengthAxis.Row);

    /// <summary>Row index for the code editor panel.</summary>
    public static readonly IMultiValueConverter CodeRowIndex =
        new SplitIndexConverter(SplitLengthRole.Code, SplitLengthAxis.Row);

    /// <summary>Column index for the designer panel.</summary>
    public static readonly IMultiValueConverter DesignerColumnIndex =
        new SplitIndexConverter(SplitLengthRole.Design, SplitLengthAxis.Column);

    /// <summary>Column index for the splitter between panels.</summary>
    public static readonly IMultiValueConverter SplitterColumnIndex =
        new SplitIndexConverter(SplitLengthRole.Splitter, SplitLengthAxis.Column);

    /// <summary>Column index for the code editor panel.</summary>
    public static readonly IMultiValueConverter CodeColumnIndex =
        new SplitIndexConverter(SplitLengthRole.Code, SplitLengthAxis.Column);

    /// <summary>Visibility for the vertical splitter.</summary>
    public static readonly IMultiValueConverter ShowVerticalSplitter =
        new SplitterVisibilityConverter(Orientation.Vertical);

    /// <summary>Visibility for the horizontal splitter.</summary>
    public static readonly IMultiValueConverter ShowHorizontalSplitter =
        new SplitterVisibilityConverter(Orientation.Horizontal);

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

    private sealed class SplitLengthConverter : IMultiValueConverter
    {
        private readonly SplitLengthRole _role;
        private readonly SplitLengthAxis _axis;

        public SplitLengthConverter(SplitLengthRole role, SplitLengthAxis axis)
        {
            _role = role;
            _axis = axis;
        }

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            DocumentViewMode mode = values.Count > 0 && values[0] is DocumentViewMode m
                ? m
                : DocumentViewMode.Split;
            Orientation orientation = values.Count > 1 && values[1] is Orientation o
                ? o
                : Orientation.Vertical;

            bool useColumns = mode == DocumentViewMode.Split && orientation == Orientation.Horizontal;
            if ((_axis == SplitLengthAxis.Column && !useColumns) || (_axis == SplitLengthAxis.Row && useColumns))
            {
                return _role == SplitLengthRole.Design
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0, GridUnitType.Pixel);
            }

            return mode switch
            {
                DocumentViewMode.Design => _role switch
                {
                    SplitLengthRole.Design => new GridLength(1, GridUnitType.Star),
                    _ => new GridLength(0, GridUnitType.Pixel)
                },
                DocumentViewMode.Code => _role switch
                {
                    SplitLengthRole.Code => new GridLength(1, GridUnitType.Star),
                    _ => new GridLength(0, GridUnitType.Pixel)
                },
                DocumentViewMode.Split => _role switch
                {
                    SplitLengthRole.Design => new GridLength(1, GridUnitType.Star),
                    SplitLengthRole.Splitter => GridLength.Auto,
                    SplitLengthRole.Code => new GridLength(1, GridUnitType.Star),
                    _ => new GridLength(0, GridUnitType.Pixel)
                },
                _ => new GridLength(1, GridUnitType.Star)
            };
        }
    }

    private sealed class SplitIndexConverter : IMultiValueConverter
    {
        private readonly SplitLengthRole _role;
        private readonly SplitLengthAxis _axis;

        public SplitIndexConverter(SplitLengthRole role, SplitLengthAxis axis)
        {
            _role = role;
            _axis = axis;
        }

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            DocumentViewMode mode = values.Count > 0 && values[0] is DocumentViewMode m
                ? m
                : DocumentViewMode.Split;
            Orientation orientation = values.Count > 1 && values[1] is Orientation o
                ? o
                : Orientation.Vertical;

            bool useColumns = mode == DocumentViewMode.Split && orientation == Orientation.Horizontal;
            if (_axis == SplitLengthAxis.Row)
            {
                return useColumns ? 0 : GetRoleIndex(_role);
            }

            return useColumns ? GetRoleIndex(_role) : 0;
        }
    }

    private sealed class OrientationEqualityConverter : IValueConverter
    {
        private readonly Orientation _target;

        public OrientationEqualityConverter(Orientation target) => _target = target;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Orientation orientation && orientation == _target;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? _target : Avalonia.Data.BindingOperations.DoNothing;
    }

    private sealed class SplitterVisibilityConverter : IMultiValueConverter
    {
        private readonly Orientation _target;

        public SplitterVisibilityConverter(Orientation target)
        {
            _target = target;
        }

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
            {
                return false;
            }

            if (values[0] is not DocumentViewMode mode || mode != DocumentViewMode.Split)
            {
                return false;
            }

            return values[1] is Orientation orientation && orientation == _target;
        }
    }

    private static int GetRoleIndex(SplitLengthRole role)
    {
        return role switch
        {
            SplitLengthRole.Design => 0,
            SplitLengthRole.Splitter => 1,
            SplitLengthRole.Code => 2,
            _ => 0
        };
    }

    private enum SplitLengthRole
    {
        Design,
        Splitter,
        Code
    }

    private enum SplitLengthAxis
    {
        Row,
        Column
    }
}
