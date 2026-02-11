namespace XamlVisualEditor.Extensions.Hosting.VscodeCompat;

/// <summary>Settings for the VS Code compatibility host.</summary>
public sealed record VscodeCompatSettings
{
    /// <summary>Gets whether the compatibility host is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the node executable path.</summary>
    public string? NodePath { get; init; }

    /// <summary>Gets the VS Code extensions root path.</summary>
    public string? ExtensionsRoot { get; init; }

    /// <summary>Gets the extension ids to load.</summary>
    public IReadOnlyList<string> ExtensionIds { get; init; } = Array.Empty<string>();
}
