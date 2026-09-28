using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.Shell.ViewModels;

internal static class WorkspaceAssemblyDiscovery
{
    public static IReadOnlyList<string> FindProjectOutputs(
        ProjectModel project,
        Action<string, Exception>? onEnumerationError = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!string.IsNullOrWhiteSpace(project.OutputAssemblyPath)
            && File.Exists(project.OutputAssemblyPath))
        {
            string? outputDirectory = Path.GetDirectoryName(project.OutputAssemblyPath);
            return string.IsNullOrWhiteSpace(outputDirectory)
                ? Array.Empty<string>()
                : EnumerateTopLevelAssemblies(outputDirectory, onEnumerationError);
        }

        if (string.IsNullOrWhiteSpace(project.ProjectPath))
        {
            return Array.Empty<string>();
        }

        string? projectDirectory = Path.GetDirectoryName(project.ProjectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return Array.Empty<string>();
        }

        IReadOnlyList<string> expectedFileNames = GetExpectedFileNames(project);
        List<string> roots = new();
        if (!string.IsNullOrWhiteSpace(project.OutputAssemblyPath))
        {
            string? outputDirectory = Path.GetDirectoryName(project.OutputAssemblyPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                roots.Add(outputDirectory);
            }
        }

        roots.Add(Path.Combine(projectDirectory, "bin", "Debug"));
        roots.Add(Path.Combine(projectDirectory, "bin", "Release"));

        HashSet<string> outputs = new(FileSystemPathComparison.Comparer);
        foreach (string root in roots.Distinct(FileSystemPathComparison.Comparer))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (string expectedFileName in expectedFileNames)
                {
                    foreach (string file in Directory.EnumerateFiles(root, expectedFileName, SearchOption.AllDirectories))
                    {
                        if (!IsIgnoredOutputPath(file))
                        {
                            outputs.Add(Path.GetFullPath(file));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                onEnumerationError?.Invoke(root, ex);
            }
        }

        return outputs.OrderBy(path => path, FileSystemPathComparison.Comparer).ToArray();
    }

    private static IReadOnlyList<string> EnumerateTopLevelAssemblies(
        string outputDirectory,
        Action<string, Exception>? onEnumerationError)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.EnumerateFiles(outputDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(IsAssemblyFile)
                .Where(path => !IsIgnoredOutputPath(path))
                .Select(Path.GetFullPath)
                .Distinct(FileSystemPathComparison.Comparer)
                .OrderBy(path => path, FileSystemPathComparison.Comparer)
                .ToArray();
        }
        catch (Exception ex)
        {
            onEnumerationError?.Invoke(outputDirectory, ex);
            return Array.Empty<string>();
        }
    }

    private static bool IsAssemblyFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetExpectedFileNames(ProjectModel project)
    {
        if (!string.IsNullOrWhiteSpace(project.OutputAssemblyPath))
        {
            string fileName = Path.GetFileName(project.OutputAssemblyPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return new[] { fileName };
            }
        }

        List<string> fileNames = new() { project.Name + ".dll" };
        if (project.IsExecutable && OperatingSystem.IsWindows())
        {
            fileNames.Add(project.Name + ".exe");
        }

        return fileNames;
    }

    internal static bool IsIgnoredOutputPath(string assemblyPath)
    {
        string fileName = Path.GetFileName(assemblyPath);
        if (fileName.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalized = assemblyPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string marker = Path.DirectorySeparatorChar.ToString();
        return normalized.Contains(marker + "ref" + marker, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(marker + "refint" + marker, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(marker + "runtimes" + marker, StringComparison.OrdinalIgnoreCase);
    }
}
