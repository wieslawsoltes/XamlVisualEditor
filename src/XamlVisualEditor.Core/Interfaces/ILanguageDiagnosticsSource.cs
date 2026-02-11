namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Emits diagnostic change notifications.
/// </summary>
public interface ILanguageDiagnosticsSource
{
    /// <summary>Raised when diagnostics for a document change.</summary>
    event EventHandler<LanguageDiagnosticsChangedEventArgs> DiagnosticsChanged;
}
