using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Designer.Rendering;

/// <summary>
/// Factory that instantiates Avalonia controls from mutable AST nodes.
/// Supports attached properties, design-mode flag, and error handling.
/// </summary>
public sealed class ControlFactory
{
    private readonly ITypeMetadataService? _metadata;
    private readonly ILogger<ControlFactory> _logger;

    /// <summary>
    /// Gets or sets whether design mode is active. When true, certain runtime behaviors are suppressed.
    /// </summary>
    public bool IsDesignMode { get; set; } = true;

    /// <summary>
    /// Creates a new control factory with optional type metadata.
    /// </summary>
    public ControlFactory(
        ITypeMetadataService? metadata = null,
        ILogger<ControlFactory>? logger = null)
    {
        _metadata = metadata;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ControlFactory>.Instance;
    }

    /// <summary>
    /// Creates an Avalonia control tree from an AST object node, including children.
    /// </summary>
    public Control? CreateControlTree(MutableAstObjectNode astNode)
    {
        Control? result = CreateControlTreeCore(astNode);
        if (result is not null)
        {
            foreach (IDesignPreviewHook hook in DesignPreviewHookRegistry.Hooks)
            {
                try
                {
                    hook.PostProcess(result, astNode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Design preview hook post-process failed: {Message}", ex.Message);
                }
            }
        }

        return result;
    }

    private Control? CreateControlTreeCore(MutableAstObjectNode astNode)
    {
        Control? preview = TryCreateDesignPreview(astNode);
        if (preview is not null)
        {
            return preview;
        }

        Control? control = CreateControl(astNode);
        if (control is null)
        {
            return null;
        }

        string? inlineText = GetInlineText(astNode);
        if (!string.IsNullOrWhiteSpace(inlineText))
        {
            if (control is TextBlock textBlock && string.IsNullOrEmpty(textBlock.Text))
            {
                textBlock.Text = inlineText;
            }
            else if (control is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = inlineText;
            }
            else if (control is ContentControl contentControl && contentControl.Content is null)
            {
                contentControl.Content = inlineText;
            }
        }

        // Recursively create children
        if (control is Panel panel)
        {
            foreach (MutableAstNode child in astNode.Children)
            {
                if (child is MutableAstObjectNode childObj)
                {
                    Control? childControl = CreateControlTree(childObj);
                    if (childControl is not null)
                    {
                        panel.Children.Add(childControl);
                    }
                }
            }
        }
        else if (control is Decorator decorator && astNode.Children.Count > 0)
        {
            MutableAstNode firstChild = astNode.Children[0];
            if (firstChild is MutableAstObjectNode firstChildObj)
            {
                decorator.Child = CreateControlTree(firstChildObj);
            }
        }
        else if (control is ContentControl cc && astNode.Children.Count > 0)
        {
            MutableAstNode firstChild = astNode.Children[0];
            if (firstChild is MutableAstObjectNode firstChildObj)
            {
                cc.Content = CreateControlTree(firstChildObj);
            }
        }
        else if (control is ItemsControl ic)
        {
            List<Control> items = new();
            foreach (MutableAstNode child in astNode.Children)
            {
                if (child is MutableAstObjectNode childObj)
                {
                    Control? childControl = CreateControlTree(childObj);
                    if (childControl is not null)
                    {
                        items.Add(childControl);
                    }
                }
            }

            ic.ItemsSource = items;
        }

        return control;
    }

    private static string? GetInlineText(MutableAstObjectNode node)
    {
        foreach (MutableAstNode child in node.Children)
        {
            if (child is MutableAstTextNode textNode)
            {
                return textNode.Text;
            }
        }

        return null;
    }

    private Control? TryCreateDesignPreview(MutableAstObjectNode astNode)
    {
        MutableAstObjectNode? previewNode = FindDesignPreviewNode(astNode);
        if (previewNode is null)
        {
            return null;
        }

        return CreateControlTree(previewNode);
    }

    private static MutableAstObjectNode? FindDesignPreviewNode(MutableAstObjectNode astNode)
    {
        foreach (MutableAstPropertyNode prop in astNode.Properties)
        {
            if (!IsDesignPreviewProperty(prop.PropertyName))
            {
                continue;
            }

            if (prop.Value is MutableAstObjectNode objNode)
            {
                return objNode;
            }

            if (prop.Value is MutableAstTextNode textNode && !string.IsNullOrWhiteSpace(textNode.Text))
            {
                return new MutableAstObjectNode
                {
                    TypeName = "TextBlock",
                    XmlNamespace = "https://github.com/avaloniaui",
                    Properties =
                    {
                        new MutableAstPropertyNode
                        {
                            PropertyName = "Text",
                            Value = new MutableAstTextNode { Text = textNode.Text }
                        }
                    }
                };
            }
        }

        return null;
    }

    private static bool IsDesignPreviewProperty(string propertyName)
    {
        return propertyName.Equals("Design.PreviewWith", StringComparison.OrdinalIgnoreCase)
            || propertyName.EndsWith(":PreviewWith", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates an Avalonia control from an AST object node.
    /// </summary>
    public Control? CreateControl(MutableAstObjectNode astNode)
    {
        Control? control = InstantiateControl(astNode);

        if (control is null)
        {
            return null;
        }

        if (IsDesignMode)
        {
            // Set design-mode tag to suppress runtime behaviors
            control.Tag = "__DesignMode__";
        }

        ApplyProperties(control, astNode);
        return control;
    }

    private Control? InstantiateControl(MutableAstObjectNode astNode)
    {
        foreach (IDesignPreviewHook hook in DesignPreviewHookRegistry.Hooks)
        {
            try
            {
                Control? hooked = hook.TryCreateControl(astNode, CreateControlTree);
                if (hooked is not null)
                {
                    return hooked;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Design preview hook create failed: {Message}", ex.Message);
            }
        }

        if (TryCreateBuiltInControl(astNode.TypeName, out Control? builtIn))
        {
            return builtIn;
        }

        return TryCreateCustomControl(astNode);
    }

    private static bool TryCreateBuiltInControl(string typeName, out Control? control)
    {
        control = typeName switch
        {
            "Window" => new ContentControl(),
            "UserControl" => new ContentControl(),
            "Grid" => new Grid(),
            "StackPanel" => new StackPanel(),
            "DockPanel" => new DockPanel(),
            "WrapPanel" => new WrapPanel(),
            "Canvas" => new Canvas(),
            "Border" => new Border(),
            "Button" => new Button(),
            "TextBlock" => new TextBlock(),
            "TextBox" => new TextBox(),
            "CheckBox" => new CheckBox(),
            "RadioButton" => new RadioButton(),
            "ComboBox" => new ComboBox(),
            "ListBox" => new ListBox(),
            "Slider" => new Slider(),
            "ProgressBar" => new ProgressBar(),
            "ScrollViewer" => new ScrollViewer(),
            "Expander" => new Expander(),
            "TabControl" => new TabControl(),
            "TabItem" => new TabItem(),
            "ContentControl" => new ContentControl(),
            "ItemsControl" => new ItemsControl(),
            "Menu" => new Menu(),
            "MenuItem" => new MenuItem(),
            "Separator" => new Separator(),
            "Image" => new Image(),
            "Panel" => new Panel(),
            "UniformGrid" => new UniformGrid(),
            "NumericUpDown" => new NumericUpDown(),
            "Calendar" => new Calendar(),
            "DatePicker" => new DatePicker(),
            "TimePicker" => new TimePicker(),
            "SplitView" => new SplitView(),
            "ToggleSwitch" => new ToggleSwitch(),
            _ => null
        };

        return control is not null;
    }

    /// <summary>
    /// Applies AST properties to an existing control, including attached properties and
    /// property elements that carry control content (e.g. custom content surfaces).
    /// </summary>
    public void ApplyProperties(Control control, MutableAstObjectNode astNode)
    {
        foreach (MutableAstPropertyNode prop in astNode.Properties)
        {
            if (TryApplyGridDefinitions(control, prop))
            {
                continue;
            }

            if (prop.Value is MutableAstTextNode textNode)
            {
                if (TryApplyDesignProperty(control, prop.PropertyName, textNode.Text))
                {
                    continue;
                }

                TrySetProperty(control, prop.PropertyName, textNode.Text);
                continue;
            }

            if (prop.Value is MutableAstObjectNode)
            {
                TrySetObjectProperty(control, astNode, prop);
            }
        }
    }

    /// <summary>
    /// Materializes a property element whose value is object content: the child tree is
    /// instantiated and assigned to the matching control-typed member. Covers custom
    /// content surfaces that are neither Panel children nor ContentControl content.
    /// </summary>
    private void TrySetObjectProperty(Control control, MutableAstObjectNode astNode, MutableAstPropertyNode property)
    {
        string? memberName = ResolveOwnPropertyName(astNode.TypeName, property.PropertyName);
        if (memberName is null)
        {
            return;
        }

        List<Control> children = new();
        foreach (MutableAstObjectNode childNode in EnumeratePropertyObjects(property.Value))
        {
            Control? child = CreateControlTree(childNode);
            if (child is not null)
            {
                children.Add(child);
            }
        }

        if (children.Count == 0)
        {
            return;
        }

        try
        {
            AvaloniaProperty? styledProperty = AvaloniaPropertyRegistry.Instance.FindRegistered(control, memberName);
            if (styledProperty is not null && typeof(Control).IsAssignableFrom(styledProperty.PropertyType))
            {
                control.SetValue(styledProperty, children[0]);
                return;
            }

            System.Reflection.PropertyInfo? clrProperty = control.GetType()
                .GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (clrProperty is null)
            {
                return;
            }

            if (clrProperty.CanWrite && typeof(Control).IsAssignableFrom(clrProperty.PropertyType))
            {
                clrProperty.SetValue(control, children[0]);
                return;
            }

            if (clrProperty.GetValue(control) is System.Collections.IList list)
            {
                foreach (Control child in children)
                {
                    list.Add(child);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Design-time content property '{Property}' failed: {Message}", memberName, ex.Message);
        }
    }

    /// <summary>
    /// Maps a property-element name to the member on the element's own type: "Body" stays
    /// "Body", "GBoxMS_Standard.Body" maps to "Body" only when the prefix is the element's
    /// type. Foreign prefixes (attached properties such as "Grid.Row") return null.
    /// </summary>
    private static string? ResolveOwnPropertyName(string elementTypeName, string propertyName)
    {
        int dotIndex = propertyName.LastIndexOf('.');
        if (dotIndex < 0)
        {
            return propertyName;
        }

        string prefix = propertyName.Substring(0, dotIndex);
        return string.Equals(prefix, elementTypeName, StringComparison.Ordinal)
            ? propertyName.Substring(dotIndex + 1)
            : null;
    }

    private static bool TryApplyGridDefinitions(Control control, MutableAstPropertyNode property)
    {
        if (control is not Grid grid)
        {
            return false;
        }

        // Only property elements carry definition objects. The attribute form
        // (RowDefinitions="Auto,*") is a text value and must reach the regular property
        // handling; consuming it here cleared the definitions and collapsed the grid.
        if (property.Value is MutableAstTextNode)
        {
            return false;
        }

        bool isColumns = property.PropertyName.EndsWith(".ColumnDefinitions", StringComparison.Ordinal)
            || property.PropertyName.Equals("ColumnDefinitions", StringComparison.Ordinal);
        bool isRows = property.PropertyName.EndsWith(".RowDefinitions", StringComparison.Ordinal)
            || property.PropertyName.Equals("RowDefinitions", StringComparison.Ordinal);
        if (!isColumns && !isRows)
        {
            return false;
        }

        IEnumerable<MutableAstObjectNode> definitions = EnumeratePropertyObjects(property.Value);
        if (isColumns)
        {
            grid.ColumnDefinitions.Clear();
            foreach (MutableAstObjectNode definition in definitions)
            {
                ColumnDefinition column = new(ParseGridLength(GetPropertyText(definition, "Width"), GridLength.Star));
                ApplyDefinitionLimits(definition, value => column.MinWidth = value, value => column.MaxWidth = value);
                grid.ColumnDefinitions.Add(column);
            }
        }
        else
        {
            grid.RowDefinitions.Clear();
            foreach (MutableAstObjectNode definition in definitions)
            {
                RowDefinition row = new(ParseGridLength(GetPropertyText(definition, "Height"), GridLength.Star));
                ApplyDefinitionLimits(definition, value => row.MinHeight = value, value => row.MaxHeight = value);
                grid.RowDefinitions.Add(row);
            }
        }

        return true;
    }

    private static IEnumerable<MutableAstObjectNode> EnumeratePropertyObjects(MutableAstNode? value)
    {
        if (value is not MutableAstObjectNode objectNode)
        {
            yield break;
        }

        if (!objectNode.TypeName.Equals("__PropertyElementChildren__", StringComparison.Ordinal))
        {
            yield return objectNode;
            yield break;
        }

        foreach (MutableAstNode child in objectNode.Children)
        {
            if (child is MutableAstObjectNode childObject)
            {
                yield return childObject;
            }
        }
    }

    private static void ApplyDefinitionLimits(
        MutableAstObjectNode definition,
        Action<double> setMinimum,
        Action<double> setMaximum)
    {
        if (double.TryParse(
                GetPropertyText(definition, "MinWidth") ?? GetPropertyText(definition, "MinHeight"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double minimum))
        {
            setMinimum(minimum);
        }

        if (double.TryParse(
                GetPropertyText(definition, "MaxWidth") ?? GetPropertyText(definition, "MaxHeight"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double maximum))
        {
            setMaximum(maximum);
        }
    }

    private static string? GetPropertyText(MutableAstObjectNode node, string propertyName)
    {
        return node.Properties
            .FirstOrDefault(property => property.PropertyName.Equals(propertyName, StringComparison.Ordinal))
            ?.Value is MutableAstTextNode text
                ? text.Text
                : null;
    }

    private static GridLength ParseGridLength(string? value, GridLength fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return GridLength.Parse(value);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Updates a single property on a control (incremental update).
    /// </summary>
    public void UpdateProperty(Control control, string propertyName, string? value)
    {
        if (value is null)
        {
            TryClearProperty(control, propertyName);
        }
        else
        {
            TrySetProperty(control, propertyName, value);
        }
    }

    /// <summary>
    /// Performs a hit-test against a control tree at the given position.
    /// Returns the deepest control at the point.
    /// </summary>
    public static Control? HitTest(Control root, Point point)
    {
        // Check children in reverse order (topmost first)
        if (root is Panel panel)
        {
            for (int i = panel.Children.Count - 1; i >= 0; i--)
            {
                if (panel.Children[i] is Control child)
                {
                    Rect childBounds = child.Bounds;
                    if (childBounds.Contains(point))
                    {
                        Point childPoint = point - childBounds.Position;
                        Control? result = HitTest(child, childPoint);
                        return result ?? child;
                    }
                }
            }
        }
        else if (root is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            Rect childBounds = decoratorChild.Bounds;
            if (childBounds.Contains(point))
            {
                Point childPoint = point - childBounds.Position;
                Control? result = HitTest(decoratorChild, childPoint);
                return result ?? decoratorChild;
            }
        }
        else if (root is ContentControl cc && cc.Content is Control content)
        {
            Rect contentBounds = content.Bounds;
            if (contentBounds.Contains(point))
            {
                Point contentPoint = point - contentBounds.Position;
                Control? result = HitTest(content, contentPoint);
                return result ?? content;
            }
        }

        return root.Bounds.Contains(point) ? root : null;
    }

    private Control? TryCreateCustomControl(MutableAstObjectNode astNode)
    {
        Control? resolved = TryCreateFromMetadata(astNode.XmlNamespace, astNode.TypeName);
        if (resolved is not null)
        {
            return resolved;
        }

        return CreatePlaceholder(astNode.TypeName);
    }

    private Control? TryCreateFromMetadata(string xmlNamespace, string typeName)
    {
        if (_metadata is null)
        {
            return null;
        }

        TypeMetadata? meta = _metadata.GetType(xmlNamespace, typeName)
            ?? _metadata.GetType(string.Empty, typeName);
        if (meta is null)
        {
            return null;
        }

        string qualifiedName = $"{meta.FullName}, {meta.AssemblyName}";
        Type? resolvedType = _metadata.ResolveClrType(meta);
        if (resolvedType is null)
        {
            resolvedType = Type.GetType(qualifiedName, throwOnError: false);
        }
        if (resolvedType is null || !typeof(Control).IsAssignableFrom(resolvedType))
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(resolvedType) as Control;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to instantiate '{Name}': {Message}", qualifiedName, ex.Message);
            return null;
        }
    }

    private static Control CreatePlaceholder(string typeName)
    {
        return new Border
        {
            MinWidth = 50,
            MinHeight = 20,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = $"[{typeName}]",
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private void TrySetProperty(Control control, string propertyName, string value)
    {
        try
        {
            // Handle attached properties first
            if (TrySetAttachedProperty(control, propertyName, value))
            {
                return;
            }

            switch (propertyName)
            {
                case "Width" when double.TryParse(value, out double w):
                    control.Width = w;
                    break;
                case "Height" when double.TryParse(value, out double h):
                    control.Height = h;
                    break;
                case "MinWidth" when double.TryParse(value, out double minW):
                    control.MinWidth = minW;
                    break;
                case "MinHeight" when double.TryParse(value, out double minH):
                    control.MinHeight = minH;
                    break;
                case "MaxWidth" when double.TryParse(value, out double maxW):
                    control.MaxWidth = maxW;
                    break;
                case "MaxHeight" when double.TryParse(value, out double maxH):
                    control.MaxHeight = maxH;
                    break;
                case "Margin":
                    control.Margin = ParseThickness(value);
                    break;
                case "Padding" when control is Decorator dec:
                    dec.Padding = ParseThickness(value);
                    break;
                case "Padding" when control is TemplatedControl tc:
                    tc.Padding = ParseThickness(value);
                    break;
                case "Opacity" when double.TryParse(value, out double opacity):
                    control.Opacity = opacity;
                    break;
                case "IsEnabled" when bool.TryParse(value, out bool isEnabled):
                    control.IsEnabled = isEnabled;
                    break;
                case "IsVisible" when bool.TryParse(value, out bool isVisible):
                    control.IsVisible = isVisible;
                    break;
                case "Name":
                    control.Name = value;
                    break;
                case "Classes":
                    control.Classes.Clear();
                    foreach (string cls in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        control.Classes.Add(cls);
                    }
                    break;
                case "Content" when control is ContentControl cc:
                    cc.Content = value;
                    break;
                case "Text" when control is TextBlock tb:
                    tb.Text = value;
                    break;
                case "Text" when control is TextBox tbox:
                    tbox.Text = value;
                    break;
                case "Header" when control is Expander exp:
                    exp.Header = value;
                    break;
                case "Header" when control is TabItem ti:
                    ti.Header = value;
                    break;
                case "HorizontalAlignment" when Enum.TryParse<HorizontalAlignment>(value, out var ha):
                    control.HorizontalAlignment = ha;
                    break;
                case "VerticalAlignment" when Enum.TryParse<VerticalAlignment>(value, out var va):
                    control.VerticalAlignment = va;
                    break;
                case "Background" when control is Panel p:
                    p.Background = ParseBrush(value);
                    break;
                case "Background" when control is TemplatedControl tc2:
                    tc2.Background = ParseBrush(value);
                    break;
                case "Foreground" when control is TemplatedControl tc3:
                    tc3.Foreground = ParseBrush(value);
                    break;
                case "BorderBrush" when control is TemplatedControl tc4:
                    tc4.BorderBrush = ParseBrush(value);
                    break;
                case "BorderThickness" when control is TemplatedControl tc5:
                    tc5.BorderThickness = ParseThickness(value);
                    break;
                case "CornerRadius" when control is TemplatedControl tc6:
                    tc6.CornerRadius = ParseCornerRadius(value);
                    break;
                case "FontSize" when control is TemplatedControl tc7 && double.TryParse(value, out double fs):
                    tc7.FontSize = fs;
                    break;
                case "FontWeight" when control is TemplatedControl tc8 && Enum.TryParse<FontWeight>(value, out var fw):
                    tc8.FontWeight = fw;
                    break;
                case "Orientation" when control is StackPanel sp && Enum.TryParse<Orientation>(value, out var ori):
                    sp.Orientation = ori;
                    break;
                case "Spacing" when control is StackPanel sp2 && double.TryParse(value, out double spacing):
                    sp2.Spacing = spacing;
                    break;
                case "RowDefinitions" when control is Grid grid:
                    grid.RowDefinitions = RowDefinitions.Parse(value);
                    break;
                case "ColumnDefinitions" when control is Grid gridCol:
                    gridCol.ColumnDefinitions = ColumnDefinitions.Parse(value);
                    break;
                case "Watermark" when control is TextBox watermarkBox:
                    watermarkBox.PlaceholderText = value;
                    break;
                case "PlaceholderText" when control is TextBox placeholderBox:
                    placeholderBox.PlaceholderText = value;
                    break;
                case "FontStyle" when control is TemplatedControl tc9 && Enum.TryParse<FontStyle>(value, out var fstyle):
                    tc9.FontStyle = fstyle;
                    break;
                case "TextWrapping" when control is TextBlock tbWrap && Enum.TryParse<TextWrapping>(value, out var wrap):
                    tbWrap.TextWrapping = wrap;
                    break;
                case "IsChecked" when control is ToggleButton toggleBtn && bool.TryParse(value, out bool isChecked):
                    toggleBtn.IsChecked = isChecked;
                    break;
                case "Minimum" when control is RangeBase rb1 && double.TryParse(value, out double min):
                    rb1.Minimum = min;
                    break;
                case "Maximum" when control is RangeBase rb2 && double.TryParse(value, out double max):
                    rb2.Maximum = max;
                    break;
                case "Value" when control is RangeBase rb3 && double.TryParse(value, out double val):
                    rb3.Value = val;
                    break;
                default:
                    TrySetDynamicProperty(control, propertyName, value);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Design-time property set failed: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Fallback for properties outside the built-in list: resolves a registered Avalonia
    /// property or a writable CLR property on the control type and converts the text value
    /// to its type. Custom workspace controls carry members that are unknown at compile
    /// time, so this is the only way their markup properties reach the design preview.
    /// </summary>
    private void TrySetDynamicProperty(Control control, string propertyName, string value)
    {
        if (propertyName.Contains('.'))
        {
            // Attached properties of foreign owners are handled by the explicit list only.
            return;
        }

        if (LooksLikeMarkupExtension(value))
        {
            // Bindings and other markup extensions have no design-time value to assign.
            return;
        }

        AvaloniaProperty? styledProperty = AvaloniaPropertyRegistry.Instance.FindRegistered(control, propertyName);
        if (styledProperty is not null && !styledProperty.IsReadOnly
            && TryConvertValue(styledProperty.PropertyType, value, out object? styledValue))
        {
            control.SetValue(styledProperty, styledValue);
            return;
        }

        System.Reflection.PropertyInfo? clrProperty = control.GetType()
            .GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (clrProperty is { CanWrite: true }
            && TryConvertValue(clrProperty.PropertyType, value, out object? clrValue))
        {
            clrProperty.SetValue(control, clrValue);
        }
    }

    /// <summary>
    /// Converts XAML attribute text to common property types. Returns false for types the
    /// design preview does not need to materialize (bindings, templates, complex objects).
    /// </summary>
    private static bool TryConvertValue(Type targetType, string text, out object? converted)
    {
        converted = null;
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (effectiveType == typeof(string) || effectiveType == typeof(object))
        {
            converted = text;
            return true;
        }

        if (effectiveType.IsEnum)
        {
            if (Enum.TryParse(effectiveType, text, ignoreCase: true, out object? enumValue))
            {
                converted = enumValue;
                return true;
            }

            return false;
        }

        try
        {
            if (effectiveType == typeof(bool) && bool.TryParse(text, out bool boolValue))
            {
                converted = boolValue;
                return true;
            }

            if (effectiveType == typeof(int) && int.TryParse(text, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int intValue))
            {
                converted = intValue;
                return true;
            }

            if (effectiveType == typeof(double) && double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double doubleValue))
            {
                converted = doubleValue;
                return true;
            }

            if (effectiveType == typeof(Thickness))
            {
                converted = ParseThickness(text);
                return true;
            }

            if (effectiveType == typeof(CornerRadius))
            {
                converted = ParseCornerRadius(text);
                return true;
            }

            if (effectiveType == typeof(GridLength))
            {
                converted = GridLength.Parse(text);
                return true;
            }

            if (effectiveType == typeof(Color) && Color.TryParse(text, out Color colorValue))
            {
                converted = colorValue;
                return true;
            }

            if (typeof(IBrush).IsAssignableFrom(effectiveType))
            {
                converted = Brush.Parse(text);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    private static bool TryApplyDesignProperty(Control control, string propertyName, string value)
    {
        if (!IsDesignPropertyName(propertyName))
        {
            return false;
        }

        if (IsDesignWidth(propertyName) && double.TryParse(value, out double width))
        {
            Design.SetWidth(control, width);
            return true;
        }

        if (IsDesignHeight(propertyName) && double.TryParse(value, out double height))
        {
            Design.SetHeight(control, height);
            return true;
        }

        if (IsDesignDataContext(propertyName))
        {
            if (!LooksLikeMarkupExtension(value))
            {
                Design.SetDataContext(control, value);
            }
            return true;
        }

        return false;
    }

    private static bool IsDesignPropertyName(string propertyName)
    {
        return propertyName.StartsWith("d:", StringComparison.OrdinalIgnoreCase)
            || propertyName.StartsWith("design:", StringComparison.OrdinalIgnoreCase)
            || propertyName.StartsWith("Design.", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Design", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesignWidth(string propertyName)
    {
        return IsPropertySuffixMatch(propertyName, "DesignWidth")
            || string.Equals(propertyName, "Design.Width", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesignHeight(string propertyName)
    {
        return IsPropertySuffixMatch(propertyName, "DesignHeight")
            || string.Equals(propertyName, "Design.Height", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesignDataContext(string propertyName)
    {
        return propertyName.StartsWith("d:", StringComparison.OrdinalIgnoreCase)
            && IsPropertySuffixMatch(propertyName, "DataContext")
            || string.Equals(propertyName, "Design.DataContext", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPropertySuffixMatch(string propertyName, string target)
    {
        if (string.Equals(propertyName, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int colonIndex = propertyName.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < propertyName.Length - 1)
        {
            string suffix = propertyName[(colonIndex + 1)..];
            if (string.Equals(suffix, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        int dotIndex = propertyName.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < propertyName.Length - 1)
        {
            string suffix = propertyName[(dotIndex + 1)..];
            if (string.Equals(suffix, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeMarkupExtension(string value)
    {
        string trimmed = value.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal);
    }

    private bool TrySetAttachedProperty(Control control, string propertyName, string value)
    {
        try
        {
            switch (propertyName)
            {
                case "Grid.Row" when int.TryParse(value, out int row):
                    Grid.SetRow(control, row);
                    return true;
                case "Grid.Column" when int.TryParse(value, out int col):
                    Grid.SetColumn(control, col);
                    return true;
                case "Grid.RowSpan" when int.TryParse(value, out int rowSpan):
                    Grid.SetRowSpan(control, rowSpan);
                    return true;
                case "Grid.ColumnSpan" when int.TryParse(value, out int colSpan):
                    Grid.SetColumnSpan(control, colSpan);
                    return true;
                case "Canvas.Left" when double.TryParse(value, out double left):
                    Canvas.SetLeft(control, left);
                    return true;
                case "Canvas.Top" when double.TryParse(value, out double top):
                    Canvas.SetTop(control, top);
                    return true;
                case "Canvas.Right" when double.TryParse(value, out double right):
                    Canvas.SetRight(control, right);
                    return true;
                case "Canvas.Bottom" when double.TryParse(value, out double bottom):
                    Canvas.SetBottom(control, bottom);
                    return true;
                case "DockPanel.Dock" when Enum.TryParse<Dock>(value, out var dock):
                    DockPanel.SetDock(control, dock);
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Attached property set failed for '{Property}': {Message}", propertyName, ex.Message);
        }

        return false;
    }

    private void TryClearProperty(Control control, string propertyName)
    {
        try
        {
            switch (propertyName)
            {
                case "Width":
                    control.Width = double.NaN;
                    break;
                case "Height":
                    control.Height = double.NaN;
                    break;
                case "Name":
                    control.Name = null;
                    break;
                case "Margin":
                    control.Margin = default;
                    break;
                case "Grid.Row":
                    Grid.SetRow(control, 0);
                    break;
                case "Grid.Column":
                    Grid.SetColumn(control, 0);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Clear property '{Property}' failed: {Message}", propertyName, ex.Message);
        }
    }

    private static Thickness ParseThickness(string value)
    {
        string[] parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 when double.TryParse(parts[0].Trim(), out double uniform) =>
                new Thickness(uniform),
            2 when double.TryParse(parts[0].Trim(), out double h) && double.TryParse(parts[1].Trim(), out double v) =>
                new Thickness(h, v),
            4 when double.TryParse(parts[0].Trim(), out double l) && double.TryParse(parts[1].Trim(), out double t)
                 && double.TryParse(parts[2].Trim(), out double r) && double.TryParse(parts[3].Trim(), out double b) =>
                new Thickness(l, t, r, b),
            _ => default
        };
    }

    private static CornerRadius ParseCornerRadius(string value)
    {
        string[] parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 when double.TryParse(parts[0].Trim(), out double uniform) =>
                new CornerRadius(uniform),
            4 when double.TryParse(parts[0].Trim(), out double tl) && double.TryParse(parts[1].Trim(), out double tr)
                 && double.TryParse(parts[2].Trim(), out double br) && double.TryParse(parts[3].Trim(), out double bl) =>
                new CornerRadius(tl, tr, br, bl),
            _ => default
        };
    }

    private IBrush? ParseBrush(string value)
    {
        try
        {
            if (Color.TryParse(value, out Color color))
            {
                return new SolidColorBrush(color);
            }

            // Try named colors (e.g., "Red", "Blue")
            return value switch
            {
                "Transparent" => Brushes.Transparent,
                "Black" => Brushes.Black,
                "White" => Brushes.White,
                "Red" => Brushes.Red,
                "Green" => Brushes.Green,
                "Blue" => Brushes.Blue,
                "Yellow" => Brushes.Yellow,
                "Orange" => Brushes.Orange,
                "Gray" => Brushes.Gray,
                "DarkGray" => Brushes.DarkGray,
                "LightGray" => Brushes.LightGray,
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to parse brush value '{Value}': {Message}", value, ex.Message);
            return null;
        }
    }
}
