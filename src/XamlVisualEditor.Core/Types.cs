namespace XamlVisualEditor.Core;

/// <summary>
/// Represents a XAML diagnostic (error, warning, or info) with location information.
/// </summary>
public sealed class XamlDiagnostic
{
    /// <summary>
    /// Gets the severity of the diagnostic.
    /// </summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the 1-based line number where the diagnostic occurs.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// Gets the 1-based column number where the diagnostic occurs.
    /// </summary>
    public required int Column { get; init; }

    /// <summary>
    /// Gets the length of the problematic text span.
    /// </summary>
    public required int Length { get; init; }

    public override string ToString() =>
        $"{Severity} ({Line},{Column}): {Message}";
}

/// <summary>
/// Represents a minimal text edit (offset + length + replacement).
/// </summary>
public sealed class TextEdit
{
    /// <summary>
    /// Gets the zero-based offset in the text where the edit begins.
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// Gets the number of characters to remove starting at <see cref="Offset"/>.
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// Gets the replacement text to insert at <see cref="Offset"/>.
    /// </summary>
    public required string NewText { get; init; }

    public override string ToString() =>
        $"Edit @{Offset} len={Length} → \"{NewText}\"";
}

/// <summary>
/// Options controlling XAML serialization output.
/// </summary>
public sealed class SerializationOptions
{
    /// <summary>
    /// Gets the string used for one level of indentation. Default is four spaces.
    /// </summary>
    public string IndentString { get; init; } = "    ";

    /// <summary>
    /// Gets whether to preserve original whitespace where possible.
    /// </summary>
    public bool PreserveWhitespace { get; init; } = true;

    /// <summary>
    /// Gets whether to preserve XML comments in the output.
    /// </summary>
    public bool PreserveComments { get; init; } = true;

    /// <summary>
    /// Gets the attribute ordering strategy.
    /// </summary>
    public AttributeOrdering AttributeOrdering { get; init; } = AttributeOrdering.Preserve;

    /// <summary>
    /// Gets the maximum line length before attributes are split across lines.
    /// </summary>
    public int MaxLineLength { get; init; } = 120;
}

/// <summary>
/// Options controlling XAML parsing behavior.
/// </summary>
public sealed class XamlParserOptions
{
    /// <summary>
    /// Gets whether to use the tolerant (GuiLabs) parser for live editing.
    /// </summary>
    public bool UseTolerantParser { get; init; }

    /// <summary>
    /// Gets known namespace aliases to preload.
    /// </summary>
    public IReadOnlyDictionary<string, string>? KnownNamespaces { get; init; }
}
