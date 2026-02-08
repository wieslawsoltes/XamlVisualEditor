namespace XamlVisualEditor.Core;

/// <summary>
/// Represents a 1-based line/column position in a document.
/// </summary>
public readonly record struct LanguageTextPosition(int Line, int Column);

/// <summary>
/// Represents a range in a document using 1-based line/column positions.
/// </summary>
public readonly record struct LanguageTextRange(LanguageTextPosition Start, LanguageTextPosition End);

/// <summary>
/// Represents a language diagnostic with location information.
/// </summary>
public sealed class LanguageDiagnostic
{
    /// <summary>Gets the severity of the diagnostic.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Gets the diagnostic message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the file path for the diagnostic.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the diagnostic range.</summary>
    public required LanguageTextRange Range { get; init; }

    /// <summary>Gets an optional diagnostic code.</summary>
    public string? Code { get; init; }

    /// <summary>Gets the diagnostic source (e.g., compiler, analyzer).</summary>
    public string? Source { get; init; }
}

/// <summary>
/// Represents a location within a document.
/// </summary>
public sealed class LanguageLocation
{
    /// <summary>Gets the file path for the location.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the range for the location.</summary>
    public required LanguageTextRange Range { get; init; }
}

/// <summary>
/// Represents hover information at a position.
/// </summary>
public sealed class LanguageHover
{
    /// <summary>Gets the hover content.</summary>
    public required string Contents { get; init; }

    /// <summary>Gets the range this hover applies to.</summary>
    public LanguageTextRange? Range { get; init; }
}

/// <summary>
/// Represents signature help information.
/// </summary>
public sealed class LanguageSignatureHelp
{
    /// <summary>Gets the available signatures.</summary>
    public required IReadOnlyList<LanguageSignature> Signatures { get; init; }

    /// <summary>Gets the active signature index.</summary>
    public int ActiveSignature { get; init; }

    /// <summary>Gets the active parameter index.</summary>
    public int ActiveParameter { get; init; }
}

/// <summary>
/// Represents rename information at a caret position.
/// </summary>
public sealed class LanguageRenameInfo
{
    /// <summary>Gets the current symbol name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the range that should be renamed.</summary>
    public required LanguageTextRange Range { get; init; }
}

/// <summary>
/// Represents edits for a single document.
/// </summary>
public sealed class LanguageDocumentEdit
{
    /// <summary>Gets the file path for the document.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the text edits for the document.</summary>
    public required IReadOnlyList<TextEdit> Edits { get; init; }
}

/// <summary>
/// Represents a workspace-wide set of edits.
/// </summary>
public sealed class LanguageWorkspaceEdit
{
    /// <summary>Gets the per-document edits.</summary>
    public required IReadOnlyList<LanguageDocumentEdit> DocumentEdits { get; init; }
}

/// <summary>
/// Represents a single callable signature.
/// </summary>
public sealed class LanguageSignature
{
    /// <summary>Gets the signature label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets optional documentation.</summary>
    public string? Documentation { get; init; }

    /// <summary>Gets the signature parameters.</summary>
    public IReadOnlyList<LanguageParameter> Parameters { get; init; } = Array.Empty<LanguageParameter>();
}

/// <summary>
/// Represents a signature parameter.
/// </summary>
public sealed class LanguageParameter
{
    /// <summary>Gets the parameter label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets optional documentation.</summary>
    public string? Documentation { get; init; }
}
