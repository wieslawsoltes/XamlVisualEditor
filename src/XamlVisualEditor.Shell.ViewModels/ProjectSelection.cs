using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.Shell.ViewModels;

internal static class ProjectSelection
{
    private static readonly string[] PreferredFrameworks =
    {
        "net10.0",
        "net9.0",
        "net8.0",
        "net7.0",
        "net6.0",
        "net5.0",
        "netcoreapp3.1",
        "netcoreapp3.0"
    };

    public static IReadOnlyList<string> FrameworkPreference => PreferredFrameworks;

    public static ProjectModel ChoosePreferredProject(ProjectModel existing, ProjectModel candidate)
    {
        if (candidate.IsExecutable && !existing.IsExecutable)
        {
            return candidate;
        }

        if (existing.IsExecutable && !candidate.IsExecutable)
        {
            return existing;
        }

        int existingRank = GetFrameworkRank(existing);
        int candidateRank = GetFrameworkRank(candidate);

        if (candidateRank < existingRank)
        {
            return candidate;
        }

        return existing;
    }

    public static ProjectModel? SelectPreferredProject(IEnumerable<ProjectModel> projects)
    {
        ProjectModel? selected = null;
        foreach (ProjectModel project in projects)
        {
            selected = selected is null ? project : ChoosePreferredProject(selected, project);
        }

        return selected;
    }

    public static IReadOnlyList<ProjectModel> DeduplicateProjectsByPath(IEnumerable<ProjectModel> projects)
    {
        Dictionary<string, ProjectModel> byPath = new(StringComparer.OrdinalIgnoreCase);
        List<ProjectModel> withoutPath = new();

        foreach (ProjectModel project in projects)
        {
            if (string.IsNullOrWhiteSpace(project.ProjectPath))
            {
                withoutPath.Add(project);
                continue;
            }

            if (byPath.TryGetValue(project.ProjectPath, out ProjectModel? existing))
            {
                byPath[project.ProjectPath] = ChoosePreferredProject(existing, project);
                continue;
            }

            byPath[project.ProjectPath] = project;
        }

        List<ProjectModel> results = new(byPath.Count + withoutPath.Count);
        foreach (ProjectModel project in byPath.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(project);
        }

        foreach (ProjectModel project in withoutPath.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(project);
        }

        return results;
    }

    public static ProjectModel? FindProjectForFile(WorkspaceModel workspace, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        ProjectModel? selected = null;
        foreach (ProjectModel project in workspace.Projects)
        {
            if (!ProjectContainsFile(project, filePath))
            {
                continue;
            }

            selected = selected is null ? project : ChoosePreferredProject(selected, project);
        }

        // SDK-style projects glob their XAML implicitly, so the explicit item lists
        // may not contain the file. Fall back to the project whose directory is the
        // closest ancestor of the file path.
        if (selected is null)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch
            {
                return null;
            }

            int bestMatchLength = -1;
            foreach (ProjectModel project in workspace.Projects)
            {
                string? projectDir = Path.GetDirectoryName(project.ProjectPath);
                if (string.IsNullOrEmpty(projectDir))
                {
                    continue;
                }

                string normalizedDir = Path.GetFullPath(projectDir)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullPath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase)
                    && normalizedDir.Length > bestMatchLength)
                {
                    bestMatchLength = normalizedDir.Length;
                    selected = project;
                }
            }
        }

        return selected;
    }

    public static string? ResolveTargetAssemblyPath(ProjectModel project)
    {
        if (!string.IsNullOrWhiteSpace(project.OutputAssemblyPath))
        {
            return project.OutputAssemblyPath;
        }

        string? projectDir = Path.GetDirectoryName(project.ProjectPath);
        if (string.IsNullOrWhiteSpace(projectDir))
        {
            return null;
        }

        string targetName = project.Name + ".dll";
        if (!string.IsNullOrWhiteSpace(project.TargetFramework))
        {
            foreach (string configuration in new[] { "Debug", "Release" })
            {
                string candidate = Path.Combine(projectDir, "bin", configuration, project.TargetFramework, targetName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        string? preferred = FindPreferredOutputAssemblyPath(projectDir, targetName);
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        string[] searchRoots =
        {
            Path.Combine(projectDir, "bin", "Debug"),
            Path.Combine(projectDir, "bin", "Release")
        };

        foreach (string root in searchRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                string? match = Directory.EnumerateFiles(root, targetName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public static int GetFrameworkRank(ProjectModel project)
    {
        if (!string.IsNullOrWhiteSpace(project.TargetFramework))
        {
            return GetFrameworkRankFromString(project.TargetFramework);
        }

        if (!string.IsNullOrWhiteSpace(project.OutputAssemblyPath))
        {
            return GetFrameworkRankFromString(project.OutputAssemblyPath);
        }

        return int.MaxValue;
    }

    public static int GetFrameworkRankFromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return int.MaxValue;
        }

        string normalized = value.Replace('\\', '/').ToLowerInvariant();
        for (int i = 0; i < PreferredFrameworks.Length; i++)
        {
            string framework = PreferredFrameworks[i];
            if (normalized.Equals(framework, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/" + framework))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static bool ProjectContainsFile(ProjectModel project, string filePath)
    {
        foreach (XamlFileModel file in project.XamlFiles)
        {
            if (string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (ProjectFileModel file in project.Files)
        {
            if (string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? FindPreferredOutputAssemblyPath(string projectDir, string targetName)
    {
        string debugRoot = Path.Combine(projectDir, "bin", "Debug");
        if (Directory.Exists(debugRoot))
        {
            foreach (string tfm in PreferredFrameworks)
            {
                string candidate = Path.Combine(debugRoot, tfm, targetName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
