namespace XamlVisualEditor.Lsp;

/// <summary>
/// Describes how to start an LSP server for a language.
/// </summary>
public sealed class LspServerConfiguration
{
    /// <summary>Gets the language identifier.</summary>
    public required string LanguageId { get; init; }

    /// <summary>Gets the server executable path.</summary>
    public required string ServerPath { get; init; }

    /// <summary>Gets the server arguments.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    /// <summary>Gets the file extensions handled by this server.</summary>
    public IReadOnlyList<string> FileExtensions { get; init; } = Array.Empty<string>();

    /// <summary>Gets the optional working directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Gets environment variables for the server process.</summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
