namespace XamlVisualEditor.Extensions;

/// <summary>Provides system file/folder icons for extension UIs.</summary>
public interface ISystemIconService
{
    /// <summary>
    /// Gets an icon for a file or folder path.
    /// </summary>
    /// <param name="path">Target file or folder path.</param>
    /// <param name="isDirectory">True for folder icons, false for file icons.</param>
    /// <param name="fallbackIcon">Fallback icon returned when native lookup is unavailable.</param>
    /// <param name="iconSize">Preferred icon size in pixels.</param>
    /// <returns>A platform icon object or the fallback icon.</returns>
    object? GetIcon(string? path, bool isDirectory, object? fallbackIcon = null, int iconSize = 16);

    /// <summary>
    /// Gets an icon for a file path.
    /// </summary>
    /// <param name="path">Target file path.</param>
    /// <param name="fallbackIcon">Fallback icon returned when native lookup is unavailable.</param>
    /// <param name="iconSize">Preferred icon size in pixels.</param>
    /// <returns>A platform icon object or the fallback icon.</returns>
    object? GetFileIcon(string? path, object? fallbackIcon = null, int iconSize = 16);

    /// <summary>
    /// Gets an icon for a folder path.
    /// </summary>
    /// <param name="path">Target folder path.</param>
    /// <param name="fallbackIcon">Fallback icon returned when native lookup is unavailable.</param>
    /// <param name="iconSize">Preferred icon size in pixels.</param>
    /// <returns>A platform icon object or the fallback icon.</returns>
    object? GetFolderIcon(string? path, object? fallbackIcon = null, int iconSize = 16);
}
