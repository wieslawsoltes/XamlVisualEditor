using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Workspace;

/// <summary>
/// Loads and manages MSBuild workspace and project information.
/// </summary>
public sealed class WorkspaceService : IWorkspaceService, IDisposable
{
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;

    /// <summary>
    /// Ensures MSBuild is located and registered.
    /// </summary>
    public static void EnsureMSBuildRegistered()
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
                Console.WriteLine($"MSBuild selected: {instance.Name} {instance.Version} at {instance.MSBuildPath}");
            }

            VisualStudioInstance? instanceToRegister = instances.FirstOrDefault();
            if (instanceToRegister is not null)
            {
                MSBuildLocator.RegisterInstance(instanceToRegister);
            }
            else
            {
                Console.WriteLine("MSBuild instances not found. Falling back to defaults.");
                MSBuildLocator.RegisterDefaults();
            }
        }
    }

    private static void EnsureDotnetRoot()
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
            Console.WriteLine($"DOTNET_ROOT set to {arm64Root} from DOTNET_ROOT_ARM64");
        }
    }

    private static void LogMsBuildInstances(IReadOnlyList<VisualStudioInstance> instances)
    {
        if (instances.Count == 0)
        {
            Console.WriteLine("MSBuild instances: none");
            return;
        }

        Console.WriteLine("MSBuild instances:");
        foreach (VisualStudioInstance instance in instances)
        {
            Console.WriteLine($"- {instance.Name} {instance.Version} at {instance.MSBuildPath}");
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
            Console.WriteLine($"Loading solution: {solutionPath}");
            Progress<ProjectLoadProgress> progress = new(p =>
                Console.WriteLine($"Workspace load: {FormatProgress(p)}"));
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
                References = references
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
            Console.WriteLine($"Loading project: {projectPath}");
            Progress<ProjectLoadProgress> progress = new(p =>
                Console.WriteLine($"Workspace load: {FormatProgress(p)}"));
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
            References = references
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
            References = Array.Empty<AssemblyReference>()
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

    private static IReadOnlyList<XamlFileModel> LoadXamlFromProjectFile(string? projectPath)
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
            System.Diagnostics.Trace.TraceWarning($"Failed to read project '{projectPath}': {ex.Message}");
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

    private static IEnumerable<string> EnumerateProjectXamlFiles(string projectDir)
    {
        IEnumerable<string> files;
        try
        {
            files = System.IO.Directory.EnumerateFiles(projectDir, "*.xaml", System.IO.SearchOption.AllDirectories)
                .Concat(System.IO.Directory.EnumerateFiles(projectDir, "*.axaml", System.IO.SearchOption.AllDirectories));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to enumerate XAML files in '{projectDir}': {ex.Message}");
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

    private static IEnumerable<string> ResolveItemFiles(string projectDir, string include, string? exclude)
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
            System.Diagnostics.Trace.TraceWarning($"Failed to enumerate '{searchRoot}': {ex.Message}");
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

    private static void OnWorkspaceFailed(WorkspaceDiagnosticEventArgs e)
    {
        if (e.Diagnostic is not null)
        {
            Console.WriteLine($"Workspace diagnostic [{e.Diagnostic.Kind}]: {e.Diagnostic.Message}");
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
        // In a full implementation, this would resolve xmlns URIs to CLR namespaces
        // and return all types from those namespaces
        return Array.Empty<TypeMetadata>();
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
        foreach (System.Reflection.PropertyInfo prop in clrType.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            PropertyKind kind = InferKind(prop.PropertyType);
            properties.Add(new PropertyMetadata
            {
                Name = prop.Name,
                TypeFullName = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                Kind = kind,
                IsReadOnly = !prop.CanWrite
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
        // In a full implementation, this would return xmlns URIs from loaded assemblies
        return Array.Empty<string>();
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
            Type? type = asm.GetType(fullTypeName, throwOnError: false);
            if (type is not null)
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
        try
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.LoadFrom(assemblyPath);
            _loadedAssemblies.Add(asm);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to load assembly '{assemblyPath}': {ex.Message}");
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

            Type? resolved = asm.GetType(type.FullName, throwOnError: false);
            if (resolved is not null)
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
            Type? type = asm.GetType(fullTypeName, throwOnError: false);
            if (type is not null)
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
        // All property kinds map to the available enum values:
        // Styled, Direct, Attached, ClrProperty
        return PropertyKind.ClrProperty;
    }

    private bool TryResolveType(string xmlNamespace, string typeName, out Type? resolved)
    {
        resolved = null;

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
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

                Type? match = asm.GetType(fullName, throwOnError: false);
                if (match is not null)
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
                System.Diagnostics.Trace.TraceWarning(
                    $"Failed to enumerate exported types from '{asm.FullName}': {ex.Message}");
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
