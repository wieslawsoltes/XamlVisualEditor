using System.Text.Json;

namespace XamlVisualEditor.Extensions;

/// <summary>Describes an extension manifest.</summary>
public sealed class ExtensionManifest
{
    /// <summary>Gets the extension name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the optional display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the extension publisher.</summary>
    public required string Publisher { get; init; }

    /// <summary>Gets the extension version.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the entry point module or assembly path.</summary>
    public string? Main { get; init; }

    /// <summary>Gets the activation events.</summary>
    public IReadOnlyList<string> ActivationEvents { get; init; } = Array.Empty<string>();

    /// <summary>Gets the engine requirements block.</summary>
    public JsonElement? Engines { get; init; }

    /// <summary>Gets the contribution definitions.</summary>
    public JsonElement? Contributes { get; init; }

    /// <summary>Gets the computed extension id.</summary>
    public string ExtensionId => Publisher + "." + Name;
}

/// <summary>Describes an extension package.</summary>
public sealed class ExtensionPackageInfo
{
    /// <summary>Creates a package info.</summary>
    public ExtensionPackageInfo(string packagePath, ExtensionManifest manifest)
    {
        PackagePath = packagePath;
        Manifest = manifest;
    }

    /// <summary>Gets the package path.</summary>
    public string PackagePath { get; }

    /// <summary>Gets the parsed manifest.</summary>
    public ExtensionManifest Manifest { get; }
}
