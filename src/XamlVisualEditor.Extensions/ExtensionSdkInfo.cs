using System;

namespace XamlVisualEditor.Extensions;

/// <summary>Provides version metadata for the extension SDK.</summary>
public static class ExtensionSdkInfo
{
    /// <summary>Gets the current SDK API version.</summary>
    public static Version ApiVersion { get; } = new(0, 1, 0);
}
