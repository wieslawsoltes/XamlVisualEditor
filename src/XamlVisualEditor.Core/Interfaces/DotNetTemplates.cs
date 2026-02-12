using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Describes a .NET template listed by the dotnet CLI.
/// </summary>
public sealed class DotNetTemplateInfo
{
    public required string Name { get; init; }

    public required string ShortName { get; init; }

    public string? Language { get; init; }

    public string? Type { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Request to create a new project from a dotnet template.
/// </summary>
public sealed class DotNetNewProjectRequest
{
    public required string TemplateShortName { get; init; }

    public required string ProjectName { get; init; }

    public required string Location { get; init; }

    public bool CreateProjectDirectory { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Request to create a solution with one or more projects.
/// </summary>
public sealed class DotNetNewSolutionRequest
{
    public required string SolutionName { get; init; }

    public required string Location { get; init; }

    public bool CreateSolutionDirectory { get; init; }

    public bool AddProjectsToSolution { get; init; } = true;

    public IReadOnlyList<DotNetNewProjectRequest> Projects { get; init; } = new List<DotNetNewProjectRequest>();
}

/// <summary>
/// Result of installing a dotnet template.
/// </summary>
public sealed class DotNetTemplateInstallResult
{
    public required bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public string? StandardOutput { get; init; }
}

/// <summary>
/// Result of creating a project or solution with dotnet new.
/// </summary>
public sealed class DotNetNewResult
{
    public required bool Success { get; init; }

    public string? ProjectPath { get; init; }

    public string? SolutionPath { get; init; }

    public string? ErrorMessage { get; init; }

    public string? StandardOutput { get; init; }
}

/// <summary>
/// Provides access to dotnet template discovery and project creation.
/// </summary>
public interface IDotNetTemplateService
{
    Task<IReadOnlyList<DotNetTemplateInfo>> ListTemplatesAsync(CancellationToken ct = default);

    Task<DotNetTemplateInstallResult> InstallTemplateAsync(string packageOrPath, CancellationToken ct = default);

    Task<DotNetNewResult> CreateProjectAsync(DotNetNewProjectRequest request, CancellationToken ct = default);

    Task<DotNetNewResult> CreateSolutionAsync(DotNetNewSolutionRequest request, CancellationToken ct = default);
}
