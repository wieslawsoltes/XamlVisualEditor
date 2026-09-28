using System;
using System.IO;

namespace XamlVisualEditor.Core;

/// <summary>Provides path comparison rules for the current operating system.</summary>
public static class FileSystemPathComparison
{
    /// <summary>Gets the comparer for filesystem path keys and collections.</summary>
    public static StringComparer Comparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>Gets the comparison mode for filesystem path values.</summary>
    public static StringComparison Comparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>Compares two filesystem paths with the rules of the current operating system.</summary>
    public static bool Equals(string? left, string? right)
    {
        return string.Equals(left, right, Comparison);
    }

    /// <summary>Returns whether a path is equal to or below a root path.</summary>
    public static bool IsSameOrDescendant(string path, string rootPath)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRootPath = Path.GetFullPath(rootPath);
        if (Equals(fullPath, fullRootPath))
        {
            return true;
        }

        string rootWithSeparator = fullRootPath.EndsWith(Path.DirectorySeparatorChar)
            || fullRootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? fullRootPath
            : fullRootPath + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, Comparison);
    }
}
