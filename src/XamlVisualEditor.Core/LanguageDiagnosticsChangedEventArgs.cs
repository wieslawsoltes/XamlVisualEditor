namespace XamlVisualEditor.Core;

/// <summary>
/// Provides data for diagnostic change notifications.
/// </summary>
public sealed class LanguageDiagnosticsChangedEventArgs : EventArgs
{
    /// <summary>Gets the document file path.</summary>
    public required string FilePath { get; init; }
}
