using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>
/// File and assembly plumbing for activating installed extension packages: the store
/// keeps the .nupkg archives only, so activation extracts the content next to the
/// archive and resolves the manifest's main assembly inside it.
/// </summary>
public static class InstalledExtensionPackages
{
    /// <summary>
    /// Extracts the package content into a version-specific content directory next to
    /// the archive. An existing directory is reused; a stale partial extraction is
    /// replaced when the archive is newer than the directory.
    /// </summary>
    public static string ExtractContent(string packagePath)
    {
        string packageDirectory = Path.GetDirectoryName(Path.GetFullPath(packagePath))
            ?? throw new InvalidOperationException("Package path has no directory: " + packagePath);
        string contentDirectory = Path.Combine(packageDirectory, "content");

        if (Directory.Exists(contentDirectory)
            && File.GetLastWriteTimeUtc(packagePath) > Directory.GetLastWriteTimeUtc(contentDirectory))
        {
            Directory.Delete(contentDirectory, recursive: true);
        }

        if (!Directory.Exists(contentDirectory))
        {
            ZipFile.ExtractToDirectory(packagePath, contentDirectory);
        }

        return contentDirectory;
    }

    /// <summary>
    /// Resolves the manifest's main assembly inside the extracted content directory.
    /// Returns null when the entry is missing, escapes the directory, or does not exist.
    /// </summary>
    public static string? ResolveMainAssemblyPath(string contentDirectory, string? mainRelativePath)
    {
        if (string.IsNullOrWhiteSpace(mainRelativePath))
        {
            return null;
        }

        string normalizedRoot = Path.GetFullPath(contentDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, mainRelativePath.Trim()));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(fullPath) ? fullPath : null;
    }

    /// <summary>
    /// Finds the activatable extension entry types of an assembly: public, non-abstract
    /// implementations of <see cref="IXveExtension"/> with a parameterless constructor.
    /// </summary>
    public static IReadOnlyList<Type> FindExtensionTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }

        return types
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.IsPublic
                && typeof(IXveExtension).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .ToArray();
    }
}
