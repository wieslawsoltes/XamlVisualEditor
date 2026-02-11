namespace XamlVisualEditor.Lsp;

/// <summary>
/// Persists LSP server configuration for the application.
/// </summary>
public interface ILspSettingsStore
{
    /// <summary>Gets the settings file path.</summary>
    string SettingsPath { get; }

    /// <summary>Loads saved LSP server configurations.</summary>
    Task<IReadOnlyList<LspServerConfiguration>> LoadAsync(CancellationToken ct = default);

    /// <summary>Saves LSP server configurations.</summary>
    Task SaveAsync(IReadOnlyList<LspServerConfiguration> servers, CancellationToken ct = default);
}
