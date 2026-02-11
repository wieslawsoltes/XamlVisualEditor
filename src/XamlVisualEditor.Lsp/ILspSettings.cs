namespace XamlVisualEditor.Lsp;

/// <summary>
/// Provides LSP server configuration settings.
/// </summary>
public interface ILspSettings
{
    /// <summary>Gets the configured LSP servers.</summary>
    IReadOnlyList<LspServerConfiguration> Servers { get; }
}
