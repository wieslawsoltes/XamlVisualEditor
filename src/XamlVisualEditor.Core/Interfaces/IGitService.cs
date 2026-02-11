using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Provides git operations for repository status, diff, and staging workflows.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Resolves the git repository root for a file or directory.
    /// </summary>
    Task<string?> GetRepositoryRootAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Gets the current repository status for the given root.
    /// </summary>
    Task<GitRepositoryStatus> GetStatusAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// Gets a unified diff for the given request.
    /// </summary>
    Task<GitDiff> GetDiffAsync(string repoPath, GitDiffRequest request, CancellationToken ct = default);

    /// <summary>
    /// Stages the specified paths.
    /// </summary>
    Task StageAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default);

    /// <summary>
    /// Unstages the specified paths.
    /// </summary>
    Task UnstageAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default);

    /// <summary>
    /// Discards changes for the specified paths.
    /// </summary>
    Task DiscardAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default);

    /// <summary>
    /// Removes untracked files for the specified paths.
    /// </summary>
    Task RemoveUntrackedAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default);

    /// <summary>
    /// Creates a commit with the given message.
    /// </summary>
    Task CommitAsync(string repoPath, string message, CancellationToken ct = default);
}
