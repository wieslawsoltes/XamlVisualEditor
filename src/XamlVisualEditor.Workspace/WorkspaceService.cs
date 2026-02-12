using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.ComponentModel;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Media;



namespace XamlVisualEditor.Workspace;

/// <summary>
/// Loads and manages MSBuild workspace and project information.
/// </summary>
public sealed class WorkspaceService : IWorkspaceService, IDisposable
{
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(ILogger<WorkspaceService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceService>.Instance;
    }

    /// <summary>
    /// Ensures MSBuild is located and registered.
    /// </summary>
    private void EnsureMSBuildRegistered()
    {
        EnsureDotnetRoot();
        if (!MSBuildLocator.IsRegistered)
        {
            IReadOnlyList<VisualStudioInstance> instances = MSBuildLocator.QueryVisualStudioInstances()
                .OrderByDescending(i => i.Version)
                .ToList();

            LogMsBuildInstances(instances);

            VisualStudioInstance? instance = instances.FirstOrDefault();
            if (instance is not null)
            {
                _logger.LogInformation("MSBuild selected: {Name} {Version} at {Path}",
                    instance.Name,
                    instance.Version,
                    instance.MSBuildPath);
            }

            VisualStudioInstance? instanceToRegister = instances.FirstOrDefault();
            if (instanceToRegister is not null)
            {
                MSBuildLocator.RegisterInstance(instanceToRegister);
            }
            else
            {
                _logger.LogInformation("MSBuild instances not found. Falling back to defaults.");
                MSBuildLocator.RegisterDefaults();
            }
        }
    }

    private void EnsureDotnetRoot()
    {
        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            return;
        }

        string? arm64Root = Environment.GetEnvironmentVariable("DOTNET_ROOT_ARM64");
        if (!string.IsNullOrWhiteSpace(arm64Root))
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", arm64Root);
            _logger.LogInformation("DOTNET_ROOT set to {Path} from DOTNET_ROOT_ARM64", arm64Root);
        }
    }

    private void LogMsBuildInstances(IReadOnlyList<VisualStudioInstance> instances)
    {
        if (instances.Count == 0)
        {
            _logger.LogInformation("MSBuild instances: none");
            return;
        }

        _logger.LogInformation("MSBuild instances:");
        foreach (VisualStudioInstance instance in instances)
        {
            _logger.LogInformation("- {Name} {Version} at {Path}",
                instance.Name,
                instance.Version,
                instance.MSBuildPath);
        }
    }

    /// <inheritdoc />
    public async Task<WorkspaceModel> LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        string? previousDirectory = Directory.GetCurrentDirectory();
        string? workspaceDirectory = Path.GetDirectoryName(solutionPath);
        if (!string.IsNullOrEmpty(workspaceDirectory))
        {
            Directory.SetCurrentDirectory(workspaceDirectory);
        }

        IDisposable? workspaceFailed = null;
        try
        {
            EnsureMSBuildRegistered();

            _workspace = MSBuildWorkspace.Create();
            workspaceFailed = _workspace.RegisterWorkspaceFailedHandler(OnWorkspaceFailed);
            _logger.LogInformation("Loading solution: {Path}", solutionPath);
            Progress<ProjectLoadProgress> progress = new(p =>
                _logger.LogInformation("Workspace load: {Progress}", FormatProgress(p)));
            _solution = await _workspace.OpenSolutionAsync(solutionPath, progress, ct);
        }
        finally
        {
            workspaceFailed?.Dispose();

            if (!string.IsNullOrEmpty(previousDirectory))
            {
                Directory.SetCurrentDirectory(previousDirectory);
            }
        }

        List<ProjectModel> projects = new();
        IReadOnlyDictionary<string, string> projectFolders = ParseSolutionFolders(solutionPath);

        foreach (Project project in _solution.Projects)
        {
            Dictionary<string, XamlFileModel> xamlFiles = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ProjectFileModel> files = new(StringComparer.OrdinalIgnoreCase);
            List<AssemblyReference> references = new();

            if (!string.IsNullOrWhiteSpace(project.FilePath))
            {
                AddProjectFile(files, project.FilePath, project.FilePath, project.Name + ".csproj");
            }

            // Find XAML files
            foreach (AdditionalDocument additional in project.AdditionalDocuments)
            {
                string filePath = additional.FilePath ?? additional.Name;
                AddProjectFile(files, project.FilePath, filePath, additional.Name);
                AddXamlFile(xamlFiles, filePath, additional.Name);
            }

            // Also check documents for XAML
            foreach (Document doc in project.Documents)
            {
                string filePath = doc.FilePath ?? doc.Name;
                AddProjectFile(files, project.FilePath, filePath, doc.Name);
                AddXamlFile(xamlFiles, filePath, doc.Name);
            }

            foreach (XamlFileModel file in LoadXamlFromProjectFile(project.FilePath))
            {
                AddXamlFile(xamlFiles, file.FilePath, file.RelativePath);
                AddProjectFile(files, project.FilePath, file.FilePath, file.RelativePath);
            }

            // Collect assembly references
            foreach (MetadataReference metaRef in project.MetadataReferences)
            {
                if (metaRef is PortableExecutableReference peRef && peRef.FilePath is not null)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(peRef.FilePath);
                    references.Add(new AssemblyReference { Name = name, Path = peRef.FilePath });
                }
            }

            projects.Add(new ProjectModel
            {
                Name = project.Name,
                ProjectPath = project.FilePath ?? string.Empty,
                XamlFiles = xamlFiles.Values.ToList(),
                Files = files.Values.ToList(),
                References = references,
                OutputAssemblyPath = project.OutputFilePath,
                TargetFramework = TryGetTargetFrameworkFromOutputPath(project.OutputFilePath),
                IsExecutable = IsExecutableProject(project)
            });
        }

        return new WorkspaceModel
        {
            Projects = projects,
            ProjectFolders = projectFolders
        };
    }

    /// <inheritdoc />
    public async Task<WorkspaceModel> LoadProjectAsync(string projectPath, CancellationToken ct = default)
    {
        string? previousDirectory = Directory.GetCurrentDirectory();
        string? workspaceDirectory = Path.GetDirectoryName(projectPath);
        if (!string.IsNullOrEmpty(workspaceDirectory))
        {
            Directory.SetCurrentDirectory(workspaceDirectory);
        }

        IDisposable? workspaceFailed = null;
        Project project;
        try
        {
            EnsureMSBuildRegistered();

            _workspace = MSBuildWorkspace.Create();
            workspaceFailed = _workspace.RegisterWorkspaceFailedHandler(OnWorkspaceFailed);
            _logger.LogInformation("Loading project: {Path}", projectPath);
            Progress<ProjectLoadProgress> progress = new(p =>
                _logger.LogInformation("Workspace load: {Progress}", FormatProgress(p)));
            project = await _workspace.OpenProjectAsync(projectPath, progress, ct);
        }
        finally
        {
            workspaceFailed?.Dispose();

            if (!string.IsNullOrEmpty(previousDirectory))
            {
                Directory.SetCurrentDirectory(previousDirectory);
            }
        }

        Dictionary<string, XamlFileModel> xamlFiles = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ProjectFileModel> files = new(StringComparer.OrdinalIgnoreCase);
        List<AssemblyReference> references = new();

        if (!string.IsNullOrWhiteSpace(project.FilePath))
        {
            AddProjectFile(files, project.FilePath, project.FilePath, project.Name + ".csproj");
        }

        foreach (Document doc in project.Documents)
        {
            string filePath = doc.FilePath ?? doc.Name;
            AddProjectFile(files, project.FilePath, filePath, doc.Name);
            AddXamlFile(xamlFiles, filePath, doc.Name);
        }

        foreach (XamlFileModel file in LoadXamlFromProjectFile(project.FilePath))
        {
            AddXamlFile(xamlFiles, file.FilePath, file.RelativePath);
            AddProjectFile(files, project.FilePath, file.FilePath, file.RelativePath);
        }

        foreach (MetadataReference metaRef in project.MetadataReferences)
        {
            if (metaRef is PortableExecutableReference peRef && peRef.FilePath is not null)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(peRef.FilePath);
                references.Add(new AssemblyReference { Name = name, Path = peRef.FilePath });
            }
        }

        ProjectModel projectModel = new()
        {
            Name = project.Name,
            ProjectPath = project.FilePath ?? string.Empty,
            XamlFiles = xamlFiles.Values.ToList(),
            Files = files.Values.ToList(),
            References = references,
            OutputAssemblyPath = project.OutputFilePath,
            TargetFramework = TryGetTargetFrameworkFromOutputPath(project.OutputFilePath),
            IsExecutable = IsExecutableProject(project)
        };

        return new WorkspaceModel
        {
            Projects = new List<ProjectModel> { projectModel },
            ProjectFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <inheritdoc />
    public WorkspaceModel CreateStandaloneWorkspace(string xamlFilePath)
    {
        string fileName = System.IO.Path.GetFileName(xamlFilePath);
        XamlFileModel xamlFile = new() { FilePath = xamlFilePath, RelativePath = fileName };
        ProjectModel project = new()
        {
            Name = fileName,
            ProjectPath = string.Empty,
            XamlFiles = new List<XamlFileModel> { xamlFile },
            Files = new List<ProjectFileModel>
            {
                new()
                {
                    FilePath = xamlFilePath,
                    RelativePath = fileName
                }
            },
            References = Array.Empty<AssemblyReference>(),
            TargetFramework = null,
            IsExecutable = false
        };

        return new WorkspaceModel
        {
            Projects = new List<ProjectModel> { project },
            ProjectFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    public void Dispose()
    {
        _workspace?.Dispose();
    }

    private static bool IsExecutableProject(Project project)
    {
        OutputKind? outputKind = project.CompilationOptions?.OutputKind;
        return outputKind == OutputKind.ConsoleApplication
               || outputKind == OutputKind.WindowsApplication
               || outputKind == OutputKind.WindowsRuntimeApplication;
    }

    private static string? TryGetTargetFrameworkFromOutputPath(string? outputFilePath)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return null;
        }

        string normalized = outputFilePath.Replace('\\', '/');
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 2; i++)
        {
            if (!string.Equals(parts[i], "bin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(parts[i + 1], "Debug", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(parts[i + 1], "Release", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string tfm = parts[i + 2];
            return string.IsNullOrWhiteSpace(tfm) ? null : tfm;
        }

        return null;
    }

    private static void AddXamlFile(
        Dictionary<string, XamlFileModel> xamlFiles,
        string filePath,
        string relativePath)
    {
        if (!filePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) &&
            !filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!xamlFiles.ContainsKey(filePath))
        {
            xamlFiles[filePath] = new XamlFileModel
            {
                FilePath = filePath,
                RelativePath = relativePath
            };
        }
    }

    private static void AddProjectFile(
        Dictionary<string, ProjectFileModel> files,
        string? projectPath,
        string filePath,
        string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (files.ContainsKey(filePath))
        {
            return;
        }

        string relative = GetRelativePath(projectPath, filePath, fallbackName);
        files[filePath] = new ProjectFileModel
        {
            FilePath = filePath,
            RelativePath = relative
        };
    }

    private static string GetRelativePath(string? projectPath, string filePath, string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return fallbackName;
        }

        string? projectDir = System.IO.Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDir))
        {
            return fallbackName;
        }

        try
        {
            return System.IO.Path.GetRelativePath(projectDir, filePath);
        }
        catch
        {
            return fallbackName;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseSolutionFolders(string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath) || !System.IO.File.Exists(solutionPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return ParseSolutionFoldersFromSlnx(solutionPath);
        }

        const string solutionFolderGuid = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";
        string solutionDir = System.IO.Path.GetDirectoryName(solutionPath) ?? string.Empty;
        Dictionary<string, string> projectPaths = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> folderNames = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> nested = new(StringComparer.OrdinalIgnoreCase);

        string[] lines;
        try
        {
            lines = System.IO.File.ReadAllLines(solutionPath);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        bool inNested = false;
        foreach (string line in lines)
        {
            if (line.StartsWith("Project(", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseProjectLine(line, out string? typeGuid, out string? name, out string? path, out string? projectGuid))
                {
                    continue;
                }

                if (string.Equals(typeGuid, solutionFolderGuid, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(projectGuid) && !string.IsNullOrWhiteSpace(name))
                    {
                        folderNames[projectGuid] = name;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(projectGuid) && !string.IsNullOrWhiteSpace(path))
                {
                    string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(solutionDir, path));
                    projectPaths[projectGuid] = fullPath;
                }

                continue;
            }

            if (line.TrimStart().StartsWith("GlobalSection(NestedProjects)", StringComparison.OrdinalIgnoreCase))
            {
                inNested = true;
                continue;
            }

            if (inNested)
            {
                if (line.TrimStart().StartsWith("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
                {
                    inNested = false;
                    continue;
                }

                string[] parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    nested[parts[0]] = parts[1];
                }
            }
        }

        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> project in projectPaths)
        {
            string projectGuid = project.Key;
            string projectPath = project.Value;
            List<string> folders = new();

            string? parent = nested.TryGetValue(projectGuid, out string? parentGuid) ? parentGuid : null;
            while (!string.IsNullOrWhiteSpace(parent) && folderNames.TryGetValue(parent, out string? folderName))
            {
                folders.Add(folderName);
                parent = nested.TryGetValue(parent, out string? next) ? next : null;
            }

            if (folders.Count > 0)
            {
                folders.Reverse();
                result[projectPath] = string.Join('/', folders);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> ParseSolutionFoldersFromSlnx(string solutionPath)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        string solutionDir = System.IO.Path.GetDirectoryName(solutionPath) ?? string.Empty;

        XDocument doc;
        try
        {
            doc = XDocument.Load(solutionPath);
        }
        catch
        {
            return result;
        }

        XElement? root = doc.Root;
        if (root is null)
        {
            return result;
        }

        foreach (XElement project in root.Elements("Project"))
        {
            string? projectPath = (string?)project.Attribute("Path");
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                continue;
            }

            string? folder = NormalizeSolutionFolderName((string?)project.Attribute("Folder"));
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(solutionDir, projectPath));
            result[fullPath] = folder;
        }

        foreach (XElement folder in root.Elements("Folder"))
        {
            VisitSlnxFolder(folder, solutionDir, string.Empty, result);
        }

        return result;
    }

    private static void VisitSlnxFolder(
        XElement folder,
        string solutionDir,
        string parentPath,
        Dictionary<string, string> result)
    {
        string? name = NormalizeSolutionFolderName((string?)folder.Attribute("Name"));
        string currentPath = CombineSolutionFolderPath(parentPath, name);

        foreach (XElement project in folder.Elements("Project"))
        {
            string? projectPath = (string?)project.Attribute("Path");
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentPath))
            {
                continue;
            }

            string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(solutionDir, projectPath));
            result[fullPath] = currentPath;
        }

        foreach (XElement childFolder in folder.Elements("Folder"))
        {
            VisitSlnxFolder(childFolder, solutionDir, currentPath, result);
        }
    }

    private static string? NormalizeSolutionFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string trimmed = name.Replace('\\', '/').Trim().Trim('/');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string CombineSolutionFolderPath(string parent, string? child)
    {
        if (string.IsNullOrWhiteSpace(child))
        {
            return parent;
        }

        if (string.IsNullOrWhiteSpace(parent))
        {
            return child;
        }

        return parent + "/" + child;
    }

    private static bool TryParseProjectLine(
        string line,
        out string? typeGuid,
        out string? name,
        out string? path,
        out string? projectGuid)
    {
        typeGuid = null;
        name = null;
        path = null;
        projectGuid = null;

        int typeStart = line.IndexOf('"');
        if (typeStart < 0)
        {
            return false;
        }

        int typeEnd = line.IndexOf('"', typeStart + 1);
        if (typeEnd < 0)
        {
            return false;
        }

        typeGuid = line.Substring(typeStart + 1, typeEnd - typeStart - 1);

        string[] parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        string[] fields = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < 3)
        {
            return false;
        }

        name = TrimQuotes(fields[0]);
        path = TrimQuotes(fields[1]);
        projectGuid = TrimQuotes(fields[2]);
        return true;
    }

    private static string TrimQuotes(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private IReadOnlyList<XamlFileModel> LoadXamlFromProjectFile(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !System.IO.File.Exists(projectPath))
        {
            return Array.Empty<XamlFileModel>();
        }

        string? projectDir = System.IO.Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(projectDir))
        {
            return Array.Empty<XamlFileModel>();
        }

        List<XamlFileModel> results = new();
        XDocument doc;
        try
        {
            doc = XDocument.Load(projectPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to read project '{Path}': {Message}", projectPath, ex.Message);
            return results;
        }

        IEnumerable<XElement> items = doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "AvaloniaXaml", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Name.LocalName, "Page", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Name.LocalName, "ApplicationDefinition", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Name.LocalName, "None", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Name.LocalName, "Content", StringComparison.OrdinalIgnoreCase));

        foreach (XElement item in items)
        {
            string? include = item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            foreach (string file in ResolveItemFiles(projectDir, include, item.Attribute("Exclude")?.Value))
            {
                string rel = System.IO.Path.GetRelativePath(projectDir, file);
                results.Add(new XamlFileModel { FilePath = file, RelativePath = rel });
            }
        }

        if (results.Count == 0)
        {
            foreach (string file in EnumerateProjectXamlFiles(projectDir))
            {
                string rel = System.IO.Path.GetRelativePath(projectDir, file);
                results.Add(new XamlFileModel { FilePath = file, RelativePath = rel });
            }
        }

        return results;
    }

    private IEnumerable<string> EnumerateProjectXamlFiles(string projectDir)
    {
        IEnumerable<string> files;
        try
        {
            files = System.IO.Directory.EnumerateFiles(projectDir, "*.xaml", System.IO.SearchOption.AllDirectories)
                .Concat(System.IO.Directory.EnumerateFiles(projectDir, "*.axaml", System.IO.SearchOption.AllDirectories));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to enumerate XAML files in '{Directory}': {Message}", projectDir, ex.Message);
            yield break;
        }

        foreach (string file in files)
        {
            if (IsIgnoredProjectPath(file))
            {
                continue;
            }

            yield return file;
        }
    }

    private static bool IsIgnoredProjectPath(string path)
    {
        return path.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\.git\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/.vs/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\.vs\\", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> ResolveItemFiles(string projectDir, string include, string? exclude)
    {
        string normalized = include.Replace('\\', '/');
        if (!HasWildcard(normalized))
        {
            string candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectDir, include));
            if (System.IO.File.Exists(candidate))
            {
                yield return candidate;
            }

            yield break;
        }

        string searchRoot = GetSearchRoot(projectDir, normalized);
        IEnumerable<string> files;
        try
        {
            files = System.IO.Directory.EnumerateFiles(searchRoot, "*.xaml", System.IO.SearchOption.AllDirectories)
                .Concat(System.IO.Directory.EnumerateFiles(searchRoot, "*.axaml", System.IO.SearchOption.AllDirectories));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to enumerate '{Root}': {Message}", searchRoot, ex.Message);
            yield break;
        }

        string pattern = normalized;
        string[] excludes = SplitPatterns(exclude);

        foreach (string file in files)
        {
            string rel = System.IO.Path.GetRelativePath(projectDir, file).Replace('\\', '/');
            if (!GlobMatch(rel, pattern))
            {
                continue;
            }

            if (excludes.Length > 0 && excludes.Any(ex => GlobMatch(rel, ex)))
            {
                continue;
            }

            yield return file;
        }
    }

    private static string GetSearchRoot(string projectDir, string pattern)
    {
        int wildcardIndex = pattern.IndexOfAny(new[] { '*', '?' });
        if (wildcardIndex <= 0)
        {
            return projectDir;
        }

        string prefix = pattern[..wildcardIndex];
        string combined = System.IO.Path.Combine(projectDir, prefix);
        string? dir = System.IO.Path.GetDirectoryName(combined);
        return string.IsNullOrEmpty(dir) ? projectDir : dir;
    }

    private static string[] SplitPatterns(string? patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
        {
            return Array.Empty<string>();
        }

        return patterns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Replace('\\', '/'))
            .ToArray();
    }

    private static bool HasWildcard(string value)
    {
        return value.Contains('*') || value.Contains('?');
    }

    private static bool GlobMatch(string text, string pattern)
    {
        string regex = Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]*")
            .Replace("\\?", "[^/]");

        return Regex.IsMatch(text, "^" + regex + "$", RegexOptions.IgnoreCase);
    }

    private void OnWorkspaceFailed(WorkspaceDiagnosticEventArgs e)
    {
        if (e.Diagnostic is not null)
        {
            _logger.LogWarning("Workspace diagnostic [{Kind}]: {Message}",
                e.Diagnostic.Kind,
                e.Diagnostic.Message);
        }
    }

    private static string FormatProgress(ProjectLoadProgress progress)
    {
        string operation = progress.Operation.ToString();
        string filePath = progress.FilePath ?? string.Empty;
        string fileName = string.IsNullOrWhiteSpace(filePath)
            ? string.Empty
            : System.IO.Path.GetFileName(filePath);
        string tfm = string.IsNullOrWhiteSpace(progress.TargetFramework)
            ? string.Empty
            : $" ({progress.TargetFramework})";

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return operation;
        }

        return $"{operation}: {fileName}{tfm}";
    }
}

/// <summary>
/// Provides type metadata from loaded assemblies for intellisense and property editing.
/// </summary>
public sealed class TypeMetadataService : ITypeMetadataService
{
    private readonly Dictionary<string, TypeMetadata> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<System.Reflection.Assembly> _loadedAssemblies = new();
    private readonly HashSet<string> _loadedAssemblyNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<TypeMetadataService> _logger;

    private readonly Dictionary<string, List<XmlnsDefinition>> _xmlnsMappings = new(StringComparer.OrdinalIgnoreCase);

    public TypeMetadataService(ILogger<TypeMetadataService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TypeMetadataService>.Instance;
        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? name = asm.GetName().Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (name.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("XamlVisualEditor", StringComparison.OrdinalIgnoreCase))
            {
                AddAssembly(asm);
            }
        }
    }

    /// <inheritdoc />
    public TypeMetadata? GetType(string xmlNamespace, string typeName)
    {
        TypeMetadata? cached = GetTypeMetadata(typeName);
        if (cached is not null)
        {
            return cached;
        }

        if (TryResolveType(xmlNamespace, typeName, out Type? resolved))
        {
            TypeMetadata meta = BuildMetadata(resolved!);
            _cache[resolved!.FullName ?? resolved.Name] = meta;
            return meta;
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<TypeMetadata> GetAvailableTypes(string? xmlNamespace = null)
    {
        if (string.IsNullOrWhiteSpace(xmlNamespace))
        {
            return Array.Empty<TypeMetadata>();
        }

        List<TypeMetadata> results = new();
        if (_xmlnsMappings.TryGetValue(xmlNamespace, out List<XmlnsDefinition>? mappings))
        {
            foreach (XmlnsDefinition mapping in mappings)
            {
                foreach (System.Reflection.Assembly asm in _loadedAssemblies)
                {
                    string? asmName = asm.GetName().Name;
                    if (!string.Equals(asmName, mapping.AssemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Type[] exportedTypes;
                    try
                    {
                        exportedTypes = asm.GetExportedTypes();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (Type type in exportedTypes)
                    {
                        if (!string.Equals(type.Namespace, mapping.ClrNamespace, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        results.Add(BuildMetadata(type));
                    }
                }
            }
        }

        return results;
    }

    /// <inheritdoc />
    public IReadOnlyList<PropertyMetadata> GetProperties(TypeMetadata type)
    {
        Type? clrType = FindClrType(type.FullName);
        if (clrType is null)
        {
            return Array.Empty<PropertyMetadata>();
        }

        List<PropertyMetadata> properties = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach ((Avalonia.AvaloniaProperty prop, Type ownerType, System.Reflection.FieldInfo field) in GetAvaloniaProperties(clrType))
        {
            string name = prop.IsAttached ? $"{ownerType.Name}.{prop.Name}" : prop.Name;
            if (!seen.Add(name))
            {
                continue;
            }

            (string category, string? description) = GetCategoryAndDescription(field);

            properties.Add(new PropertyMetadata
            {
                Name = name,
                TypeFullName = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                Kind = MapValueKind(prop.PropertyType),
                IsReadOnly = prop.IsReadOnly,
                DefaultValue = TryGetDefaultValue(prop, ownerType),
                ClrType = prop.PropertyType,
                IsAttached = prop.IsAttached,
                OwnerType = prop.IsAttached ? ownerType.FullName : null,
                Category = category,
                Description = description
            });
        }

        foreach (PropertyMetadata attached in GetAttachedProperties())
        {
            if (!seen.Add(attached.Name))
            {
                continue;
            }

            properties.Add(attached);
        }

        foreach (System.Reflection.PropertyInfo prop in clrType.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (!seen.Add(prop.Name))
            {
                continue;
            }

            (string category, string? description) = GetCategoryAndDescription(prop);

            properties.Add(new PropertyMetadata
            {
                Name = prop.Name,
                TypeFullName = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                Kind = MapValueKind(prop.PropertyType),
                IsReadOnly = !prop.CanWrite,
                ClrType = prop.PropertyType,
                Category = category,
                Description = description
            });
        }

        return properties;
    }

    /// <inheritdoc />
    public IReadOnlyList<EventMetadata> GetEvents(TypeMetadata type)
    {
        Type? clrType = FindClrType(type.FullName);
        if (clrType is null)
        {
            return Array.Empty<EventMetadata>();
        }

        List<EventMetadata> events = new();
        foreach (System.Reflection.EventInfo evt in clrType.GetEvents(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            events.Add(new EventMetadata
            {
                Name = evt.Name,
                HandlerTypeFullName = evt.EventHandlerType?.FullName ?? "EventHandler"
            });
        }

        return events;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableNamespaces()
    {
        return _xmlnsMappings.Keys.ToList();
    }

    /// <summary>
    /// Resolves a type by full CLR name.
    /// </summary>
    public TypeMetadata? GetTypeMetadata(string fullTypeName)
    {
        if (_cache.TryGetValue(fullTypeName, out TypeMetadata? cached))
        {
            return cached;
        }

        // Search loaded assemblies
        foreach (System.Reflection.Assembly asm in _loadedAssemblies)
        {
            if (TryGetTypeSafe(asm, fullTypeName, out Type? type) && type is not null)
            {
                TypeMetadata meta = BuildMetadata(type);
                _cache[fullTypeName] = meta;
                return meta;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads an assembly for metadata lookup.
    /// </summary>
    public void LoadAssembly(string assemblyPath)
    {
        if (!ShouldLoadAssembly(assemblyPath, out _))
        {
            return;
        }

        try
        {
            System.Reflection.Assembly asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            AddAssembly(asm);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load assembly '{Path}': {Message}", assemblyPath, ex.Message);
        }
    }

    /// <summary>
    /// Loads multiple assemblies for metadata lookup.
    /// </summary>
    public void LoadAssemblies(IEnumerable<string> assemblyPaths)
    {
        foreach (string path in assemblyPaths)
        {
            LoadAssembly(path);
        }
    }

    private bool ShouldLoadAssembly(string assemblyPath, out string? assemblyName)
    {
        assemblyName = null;
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assemblyPath);
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsReferenceAssemblyPath(assemblyPath))
        {
            return false;
        }

        if (!IsManagedAssembly(assemblyPath))
        {
            return false;
        }

        try
        {
            assemblyName = System.Reflection.AssemblyName.GetAssemblyName(assemblyPath).Name;
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        if (_loadedAssemblyNames.Contains(assemblyName))
        {
            if (TryGetLoadedAssembly(assemblyName, out System.Reflection.Assembly? loaded) &&
                !string.IsNullOrWhiteSpace(loaded?.Location) &&
                !string.Equals(loaded.Location, assemblyPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Assembly '{Name}' already loaded from '{LoadedPath}'. Skipping '{Path}'.",
                    assemblyName,
                    loaded.Location,
                    assemblyPath);
            }
            return false;
        }

        return true;
    }

    private static bool IsReferenceAssemblyPath(string assemblyPath)
    {
        string normalized = assemblyPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string marker = Path.DirectorySeparatorChar.ToString();
        return normalized.Contains(marker + "ref" + marker, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(marker + "refint" + marker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedAssembly(string assemblyPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(assemblyPath);
            using PEReader reader = new(stream);
            return reader.HasMetadata && reader.PEHeaders?.CorHeader is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Type? ResolveClrType(TypeMetadata type)
    {
        foreach (System.Reflection.Assembly asm in _loadedAssemblies)
        {
            if (!string.IsNullOrWhiteSpace(type.AssemblyName))
            {
                string? asmName = asm.GetName().Name;
                if (!string.Equals(asmName, type.AssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (TryGetTypeSafe(asm, type.FullName, out Type? resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private Type? FindClrType(string fullTypeName)
    {
        foreach (System.Reflection.Assembly asm in _loadedAssemblies)
        {
            if (TryGetTypeSafe(asm, fullTypeName, out Type? type))
            {
                return type;
            }
        }

        return null;
    }

    private static TypeMetadata BuildMetadata(Type type)
    {
        string clrNamespace = type.Namespace ?? string.Empty;
        string assemblyName = type.Assembly.GetName().Name ?? string.Empty;

        return new TypeMetadata
        {
            FullName = type.FullName ?? type.Name,
            Name = type.Name,
            XmlNamespace = $"clr-namespace:{clrNamespace};assembly={assemblyName}",
            ClrNamespace = clrNamespace,
            AssemblyName = assemblyName
        };
    }

    private static PropertyKind InferKind(Type propertyType)
    {
        return MapValueKind(propertyType);
    }

    private void AddAssembly(System.Reflection.Assembly asm)
    {
        string? name = asm.GetName().Name;
        if (string.IsNullOrWhiteSpace(name) || !_loadedAssemblyNames.Add(name))
        {
            return;
        }

        _loadedAssemblies.Add(asm);
        AddXmlnsDefinitions(asm);
    }

    private void AddXmlnsDefinitions(System.Reflection.Assembly asm)
    {
        string? asmName = asm.GetName().Name;
        if (string.IsNullOrWhiteSpace(asmName))
        {
            return;
        }

        foreach (System.Reflection.CustomAttributeData attr in asm.GetCustomAttributesData())
        {
            if (!IsXmlnsDefinitionAttribute(attr.AttributeType))
            {
                continue;
            }

            if (attr.ConstructorArguments.Count < 2)
            {
                continue;
            }

            string? xmlNamespace = attr.ConstructorArguments[0].Value as string;
            string? clrNamespace = attr.ConstructorArguments[1].Value as string;
            if (string.IsNullOrWhiteSpace(xmlNamespace) || string.IsNullOrWhiteSpace(clrNamespace))
            {
                continue;
            }

            if (!_xmlnsMappings.TryGetValue(xmlNamespace, out List<XmlnsDefinition>? list))
            {
                list = new List<XmlnsDefinition>();
                _xmlnsMappings[xmlNamespace] = list;
            }

            list.Add(new XmlnsDefinition(xmlNamespace, clrNamespace, asmName));
        }
    }

    private static bool IsXmlnsDefinitionAttribute(Type attributeType)
    {
        return string.Equals(attributeType.Name, "XmlnsDefinitionAttribute", StringComparison.Ordinal)
            || attributeType.FullName?.EndsWith(".XmlnsDefinitionAttribute", StringComparison.Ordinal) == true;
    }

    private bool TryGetLoadedAssembly(string assemblyName, out System.Reflection.Assembly? assembly)
    {
        foreach (System.Reflection.Assembly asm in _loadedAssemblies)
        {
            string? name = asm.GetName().Name;
            if (string.Equals(name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                assembly = asm;
                return true;
            }
        }

        assembly = null;
        return false;
    }

    private IEnumerable<(Avalonia.AvaloniaProperty Prop, Type OwnerType, System.Reflection.FieldInfo Field)> GetAvaloniaProperties(Type type)
    {
        Type? current = type;
        while (current is not null)
        {
            foreach (System.Reflection.FieldInfo field in current.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (!typeof(Avalonia.AvaloniaProperty).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                if (field.GetValue(null) is Avalonia.AvaloniaProperty prop)
                {
                    yield return (prop, prop.OwnerType ?? current, field);
                }
            }

            current = current.BaseType;
        }
    }

    private static (string Category, string? Description) GetCategoryAndDescription(System.Reflection.MemberInfo member)
    {
        string category = "Misc";
        string? description = null;

        if (member.GetCustomAttribute<CategoryAttribute>() is { } cat)
        {
            if (!string.IsNullOrWhiteSpace(cat.Category))
            {
                category = cat.Category;
            }
        }

        if (member.GetCustomAttribute<DescriptionAttribute>() is { } desc)
        {
            description = desc.Description;
        }

        return (category, description);
    }

    private IReadOnlyList<PropertyMetadata> GetAttachedProperties()
    {
        List<PropertyMetadata> attached = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (System.Reflection.Assembly asm in _loadedAssemblies)
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (Type type in types)
            {
                if (type.ContainsGenericParameters)
                {
                    continue;
                }

                foreach (System.Reflection.FieldInfo field in type.GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.FlattenHierarchy))
                {
                    try
                    {
                        if (!typeof(Avalonia.AvaloniaProperty).IsAssignableFrom(field.FieldType))
                        {
                            continue;
                        }

                        Avalonia.AvaloniaProperty? prop = field.GetValue(null) as Avalonia.AvaloniaProperty;
                        if (prop is null || !prop.IsAttached)
                        {
                            continue;
                        }

                        string name = $"{type.Name}.{prop.Name}";
                        if (!seen.Add(name))
                        {
                            continue;
                        }

                        (string category, string? description) = GetCategoryAndDescription(field);

                        attached.Add(new PropertyMetadata
                        {
                            Name = name,
                            TypeFullName = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                            Kind = MapValueKind(prop.PropertyType),
                            IsReadOnly = prop.IsReadOnly,
                            DefaultValue = TryGetDefaultValue(prop, type),
                            ClrType = prop.PropertyType,
                            IsAttached = true,
                            OwnerType = type.FullName,
                            Category = category,
                            Description = description
                        });
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        return attached;
    }

    private static object? TryGetDefaultValue(Avalonia.AvaloniaProperty prop, Type ownerType)
    {
        try
        {
            System.Reflection.MethodInfo? getDefault = prop.GetType().GetMethod("GetDefaultValue");
            if (getDefault is not null)
            {
                return getDefault.Invoke(prop, new object?[] { ownerType });
            }

            System.Reflection.MethodInfo? getMetadata = prop.GetType().GetMethod("GetMetadata");
            if (getMetadata is not null)
            {
                object? metadata = getMetadata.Invoke(prop, new object?[] { ownerType });
                if (metadata is not null)
                {
                    System.Reflection.PropertyInfo? defaultProp = metadata.GetType().GetProperty("DefaultValue");
                    if (defaultProp is not null)
                    {
                        return defaultProp.GetValue(metadata);
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static PropertyKind MapValueKind(Type propertyType)
    {
        if (propertyType == typeof(string))
        {
            return PropertyKind.String;
        }

        if (propertyType == typeof(bool) || propertyType == typeof(bool?))
        {
            return PropertyKind.Boolean;
        }

        if (propertyType.IsEnum)
        {
            return PropertyKind.Enum;
        }

        if (propertyType == typeof(Avalonia.Thickness))
        {
            return PropertyKind.Thickness;
        }

        if (propertyType == typeof(Avalonia.CornerRadius))
        {
            return PropertyKind.CornerRadius;
        }

        if (propertyType == typeof(Avalonia.Point))
        {
            return PropertyKind.Point;
        }

        if (propertyType == typeof(Avalonia.Size))
        {
            return PropertyKind.Size;
        }

        if (propertyType == typeof(Avalonia.Rect))
        {
            return PropertyKind.Rect;
        }

        if (propertyType == typeof(Avalonia.Controls.GridLength))
        {
            return PropertyKind.GridLength;
        }

        if (propertyType == typeof(Avalonia.Media.Color))
        {
            return PropertyKind.Color;
        }

        if (typeof(Avalonia.Media.IBrush).IsAssignableFrom(propertyType))
        {
            return PropertyKind.Brush;
        }

        if (propertyType == typeof(Avalonia.Media.FontFamily))
        {
            return PropertyKind.FontFamily;
        }

        if (propertyType == typeof(Avalonia.Media.FontWeight))
        {
            return PropertyKind.FontWeight;
        }

        if (propertyType == typeof(Avalonia.Media.FontStyle))
        {
            return PropertyKind.FontStyle;
        }

        if (propertyType == typeof(TimeSpan) || propertyType == typeof(TimeSpan?))
        {
            return PropertyKind.TimeSpan;
        }

        if (propertyType == typeof(Uri))
        {
            return PropertyKind.Uri;
        }

        if (typeof(Avalonia.Controls.Templates.IDataTemplate).IsAssignableFrom(propertyType) ||
            typeof(Avalonia.Controls.Templates.IControlTemplate).IsAssignableFrom(propertyType))
        {
            return PropertyKind.Template;
        }

        if (typeof(Avalonia.Markup.Xaml.MarkupExtension).IsAssignableFrom(propertyType))
        {
            return PropertyKind.MarkupExtension;
        }

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
        {
            return PropertyKind.Collection;
        }

        if (propertyType.IsPrimitive)
        {
            return PropertyKind.Numeric;
        }

        switch (Type.GetTypeCode(propertyType))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return PropertyKind.Numeric;
        }

        return PropertyKind.Object;
    }

    private bool TryResolveType(string xmlNamespace, string typeName, out Type? resolved)
    {
        resolved = null;

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(xmlNamespace) &&
            _xmlnsMappings.TryGetValue(xmlNamespace, out List<XmlnsDefinition>? mappings))
        {
            foreach (XmlnsDefinition mapping in mappings)
            {
                string fullName = typeName.Contains('.')
                    ? typeName
                    : string.IsNullOrWhiteSpace(mapping.ClrNamespace)
                        ? typeName
                        : mapping.ClrNamespace + "." + typeName;

                foreach (System.Reflection.Assembly asm in _loadedAssemblies)
                {
                    string? asmName = asm.GetName().Name;
                    if (!string.Equals(asmName, mapping.AssemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (TryGetTypeSafe(asm, fullName, out Type? match))
                    {
                        resolved = match;
                        return true;
                    }
                }
            }
        }

        if (TryParseClrNamespace(xmlNamespace, out string? clrNamespace, out string? assemblyName))
        {
            string fullName = typeName.Contains('.')
                ? typeName
                : string.IsNullOrEmpty(clrNamespace)
                    ? typeName
                    : clrNamespace + "." + typeName;

            foreach (System.Reflection.Assembly asm in _loadedAssemblies)
            {
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    string? asmName = asm.GetName().Name;
                    if (!string.Equals(asmName, assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (TryGetTypeSafe(asm, fullName, out Type? match))
                {
                    resolved = match;
                    return true;
                }
            }
        }

        foreach (System.Reflection.Assembly asm in _loadedAssemblies)
        {
            Type[] exportedTypes;
            try
            {
                exportedTypes = asm.GetExportedTypes();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Failed to enumerate exported types from '{Assembly}': {Message}",
                    asm.FullName,
                    ex.Message);
                continue;
            }

            foreach (Type type in exportedTypes)
            {
                if (string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    resolved = type;
                    return true;
                }
            }
        }

        return false;
    }

    private readonly record struct XmlnsDefinition(string XmlNamespace, string ClrNamespace, string AssemblyName);

    private bool TryGetTypeSafe(System.Reflection.Assembly asm, string typeName, out Type? type)
    {
        type = null;
        try
        {
            type = asm.GetType(typeName, throwOnError: false);
            return type is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Failed to resolve type '{TypeName}' from '{Assembly}': {Message}",
                typeName,
                asm.FullName,
                ex.Message);
            return false;
        }
    }

    private static bool TryParseClrNamespace(string xmlNamespace, out string? clrNamespace, out string? assemblyName)
    {
        clrNamespace = null;
        assemblyName = null;

        if (string.IsNullOrWhiteSpace(xmlNamespace))
        {
            return false;
        }

        string value = xmlNamespace.Trim();
        if (value.StartsWith("clr-namespace:", StringComparison.OrdinalIgnoreCase))
        {
            string remainder = value["clr-namespace:".Length..];
            ParseNamespaceParts(remainder, out clrNamespace, out assemblyName);
            return true;
        }

        if (value.StartsWith("using:", StringComparison.OrdinalIgnoreCase))
        {
            string remainder = value["using:".Length..];
            ParseNamespaceParts(remainder, out clrNamespace, out assemblyName);
            return true;
        }

        return false;
    }

    private static void ParseNamespaceParts(string value, out string? clrNamespace, out string? assemblyName)
    {
        clrNamespace = null;
        assemblyName = null;

        string[] parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 0)
        {
            clrNamespace = parts[0];
        }

        foreach (string part in parts)
        {
            if (part.StartsWith("assembly=", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = part["assembly=".Length..];
            }
        }
    }
}
