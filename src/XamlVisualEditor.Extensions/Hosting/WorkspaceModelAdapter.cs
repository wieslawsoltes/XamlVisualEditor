using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Default workspace model adapter for extension context.</summary>
public sealed class WorkspaceModelAdapter : IWorkspaceModel, IDisposable
{
    private static readonly string[] ProjectExtensions = { ".csproj", ".fsproj", ".vbproj" };
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IWorkspaceCommands _workspaceCommands;
    private readonly IWorkspaceService _workspaceService;
    private readonly object _gate = new();
    private string? _cachedWorkspacePath;
    private IReadOnlyList<WorkspaceProjectInfo> _cachedProjects = Array.Empty<WorkspaceProjectInfo>();

    public WorkspaceModelAdapter(
        IWorkspaceInfo workspaceInfo,
        IWorkspaceCommands workspaceCommands,
        IWorkspaceService workspaceService)
    {
        _workspaceInfo = workspaceInfo;
        _workspaceCommands = workspaceCommands;
        _workspaceService = workspaceService;
        _workspaceInfo.WorkspaceChanged += OnWorkspaceChanged;
    }

    public bool HasWorkspace => _workspaceCommands.HasWorkspace;

    public string? WorkspacePath => _workspaceInfo.WorkspacePath;

    public event EventHandler<WorkspaceModelChangedEventArgs>? Changed;

    public async Task<IReadOnlyList<WorkspaceProjectInfo>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? workspacePath = WorkspacePath;
        if (!HasWorkspace || string.IsNullOrWhiteSpace(workspacePath))
        {
            return Array.Empty<WorkspaceProjectInfo>();
        }

        lock (_gate)
        {
            if (string.Equals(_cachedWorkspacePath, workspacePath, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedProjects;
            }
        }

        WorkspaceModel workspace = await LoadWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<WorkspaceProjectInfo> projects = MapProjects(workspace);

        lock (_gate)
        {
            _cachedWorkspacePath = workspacePath;
            _cachedProjects = projects;
        }

        return projects;
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return _workspaceCommands.LoadWorkspaceAsync(cancellationToken);
    }

    public Task RestoreAsync(CancellationToken cancellationToken)
    {
        return _workspaceCommands.RestoreWorkspaceAsync(cancellationToken);
    }

    public Task BuildAsync(CancellationToken cancellationToken)
    {
        return _workspaceCommands.BuildWorkspaceAsync(cancellationToken);
    }

    public Task RebuildAsync(CancellationToken cancellationToken)
    {
        return _workspaceCommands.RebuildWorkspaceAsync(cancellationToken);
    }

    public Task CleanAsync(CancellationToken cancellationToken)
    {
        return _workspaceCommands.CleanWorkspaceAsync(cancellationToken);
    }

    public Task SetStartupProjectAsync(string projectPath, string? targetFramework, CancellationToken cancellationToken)
    {
        return _workspaceCommands.SetStartupProjectAsync(projectPath, targetFramework, cancellationToken);
    }

    public void Dispose()
    {
        _workspaceInfo.WorkspaceChanged -= OnWorkspaceChanged;
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        lock (_gate)
        {
            _cachedWorkspacePath = null;
            _cachedProjects = Array.Empty<WorkspaceProjectInfo>();
        }

        Changed?.Invoke(this, new WorkspaceModelChangedEventArgs(e.WorkspacePath, HasWorkspace));
    }

    private async Task<WorkspaceModel> LoadWorkspaceAsync(string workspacePath, CancellationToken cancellationToken)
    {
        if (File.Exists(workspacePath))
        {
            if (workspacePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return await _workspaceService.LoadSolutionAsync(workspacePath, cancellationToken).ConfigureAwait(false);
            }

            if (IsProjectFile(workspacePath))
            {
                return await _workspaceService.LoadProjectAsync(workspacePath, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (Directory.Exists(workspacePath))
        {
            string? solutionPath = Directory
                .EnumerateFiles(workspacePath, "*.sln", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                return await _workspaceService.LoadSolutionAsync(solutionPath, cancellationToken).ConfigureAwait(false);
            }

            string? projectPath = Directory
                .EnumerateFiles(workspacePath, "*.*proj", SearchOption.AllDirectories)
                .Where(IsProjectFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                return await _workspaceService.LoadProjectAsync(projectPath, cancellationToken).ConfigureAwait(false);
            }
        }

        return new WorkspaceModel
        {
            Projects = Array.Empty<ProjectModel>(),
            ProjectFolders = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        };
    }

    private static IReadOnlyList<WorkspaceProjectInfo> MapProjects(WorkspaceModel workspace)
    {
        if (workspace.Projects.Count == 0)
        {
            return Array.Empty<WorkspaceProjectInfo>();
        }

        string startupProjectPath = workspace.Projects
            .FirstOrDefault(project => project.IsExecutable)?.ProjectPath
            ?? workspace.Projects[0].ProjectPath;

        List<WorkspaceProjectInfo> projects = new(workspace.Projects.Count);
        foreach (ProjectModel project in workspace.Projects)
        {
            projects.Add(new WorkspaceProjectInfo(
                project.Name,
                project.ProjectPath,
                project.TargetFramework,
                string.Equals(project.ProjectPath, startupProjectPath, StringComparison.OrdinalIgnoreCase)));
        }

        return projects;
    }

    private static bool IsProjectFile(string path)
    {
        string extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        foreach (string supportedExtension in ProjectExtensions)
        {
            if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
