using System;
using System.IO;

namespace XamlVisualEditor.Tests.Unit;

/// <summary>Probes file-system case sensitivity where a test depends on it.</summary>
internal static class FileSystemCasing
{
    /// <summary>
    /// Returns true when files that differ only by case are distinct entries in
    /// <paramref name="directory"/>. An OS check is not enough: macOS is usually
    /// case-insensitive and Linux mounts can be either.
    /// </summary>
    public static bool IsCaseSensitive(string directory)
    {
        Directory.CreateDirectory(directory);
        string probePath = Path.Combine(directory, "casing-probe-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(probePath, string.Empty);
            string upperCasePath = Path.Combine(directory, Path.GetFileName(probePath).ToUpperInvariant());
            return !File.Exists(upperCasePath);
        }
        finally
        {
            File.Delete(probePath);
        }
    }
}
