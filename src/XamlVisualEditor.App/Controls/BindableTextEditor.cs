using System;
using Avalonia;
using AvaloniaEdit;

namespace XamlVisualEditor.App.Controls;

/// <summary>
/// AvaloniaEdit TextEditor with a bindable text property for compiled bindings.
/// </summary>
public sealed class BindableTextEditor : TextEditor
{
    public static readonly StyledProperty<string?> TextContentProperty =
        AvaloniaProperty.Register<BindableTextEditor, string?>(nameof(TextContent));

    static BindableTextEditor()
    {
        TextContentProperty.Changed.AddClassHandler<BindableTextEditor>((editor, args) =>
            editor.OnTextContentChanged(args));
    }

    public string? TextContent
    {
        get => GetValue(TextContentProperty);
        set => SetValue(TextContentProperty, value);
    }

    private void OnTextContentChanged(AvaloniaPropertyChangedEventArgs args)
    {
        string newText = args.NewValue as string ?? string.Empty;
        if (!string.Equals(Text, newText, StringComparison.Ordinal))
        {
            Text = newText;
        }
    }
}
