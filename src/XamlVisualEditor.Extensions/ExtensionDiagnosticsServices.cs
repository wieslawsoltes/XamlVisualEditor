using XamlVisualEditor.Core;

namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to language diagnostics.</summary>
public interface IDiagnosticsService
{
    /// <summary>Gets diagnostics for a file or the active workspace.</summary>
    Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct);

    /// <summary>Raised when diagnostics change.</summary>
    event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;
}

/// <summary>Diagnostic change notification.</summary>
public sealed class DiagnosticsChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DiagnosticsChangedEventArgs(string? filePath)
    {
        FilePath = filePath;
    }

    /// <summary>Gets the file path that changed (null for global).</summary>
    public string? FilePath { get; }
}
