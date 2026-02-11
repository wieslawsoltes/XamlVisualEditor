namespace XamlVisualEditor.Core;

/// <summary>
/// Describes the workspace context for language services.
/// </summary>
public sealed class LanguageWorkspaceInfo
{
    /// <summary>Gets the workspace root path.</summary>
    public required string RootPath { get; init; }

    /// <summary>Gets the optional solution file path.</summary>
    public string? SolutionPath { get; init; }

    /// <summary>Gets the optional project file path.</summary>
    public string? ProjectPath { get; init; }

    /// <summary>Gets the workspace kind.</summary>
    public WorkspaceKind Kind { get; init; } = WorkspaceKind.Folder;
}

/// <summary>
/// Defines the workspace kinds used for language services.
/// </summary>
public enum WorkspaceKind
{
    /// <summary>Represents a folder-based workspace.</summary>
    Folder,

    /// <summary>Represents a solution-based workspace.</summary>
    Solution,

    /// <summary>Represents a project-based workspace.</summary>
    Project,

    /// <summary>Represents a single-file workspace.</summary>
    File
}
