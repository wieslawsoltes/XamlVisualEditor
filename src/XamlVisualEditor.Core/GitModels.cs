using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Core;

/// <summary>
/// Represents a git repository status snapshot.
/// </summary>
public sealed class GitRepositoryStatus
{
    public required string RepositoryRoot { get; init; }

    public string BranchName { get; init; } = string.Empty;

    public string? UpstreamName { get; init; }

    public int AheadBy { get; init; }

    public int BehindBy { get; init; }

    public bool IsRepository { get; init; } = true;

    public string? ErrorMessage { get; init; }

    public required IReadOnlyList<GitFileChange> Changes { get; init; }
}

/// <summary>
/// Describes a file change entry in git status.
/// </summary>
public sealed class GitFileChange
{
    public required string Path { get; init; }

    public string? OldPath { get; init; }

    public GitChangeKind IndexStatus { get; init; }

    public GitChangeKind WorkTreeStatus { get; init; }

    public bool IsUntracked { get; init; }

    public bool IsIgnored { get; init; }

    public bool IsRenamed { get; init; }

    public bool IsCopied { get; init; }
}

/// <summary>
/// Known git status kinds for index or work tree.
/// </summary>
public enum GitChangeKind
{
    None,
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Unmerged,
    Untracked,
    Ignored,
    Unknown
}

/// <summary>
/// Request for a git diff operation.
/// </summary>
public sealed class GitDiffRequest
{
    public bool Staged { get; init; }

    public string? Path { get; init; }

    public bool IsUntracked { get; init; }
}

/// <summary>
/// Represents a parsed git diff.
/// </summary>
public sealed class GitDiff
{
    public required IReadOnlyList<GitDiffFile> Files { get; init; }

    public string? RawText { get; init; }
}

/// <summary>
/// Represents a file diff entry.
/// </summary>
public sealed class GitDiffFile
{
    public required string Path { get; init; }

    public string? OldPath { get; init; }

    public bool IsBinary { get; init; }

    public required IReadOnlyList<string> HeaderLines { get; init; }

    public required IReadOnlyList<GitDiffHunk> Hunks { get; init; }
}

/// <summary>
/// Represents a diff hunk.
/// </summary>
public sealed class GitDiffHunk
{
    public required string Header { get; init; }

    public int OldStart { get; init; }

    public int OldCount { get; init; }

    public int NewStart { get; init; }

    public int NewCount { get; init; }

    public required IReadOnlyList<GitDiffLine> Lines { get; init; }
}

/// <summary>
/// Represents a diff line in a hunk.
/// </summary>
public sealed class GitDiffLine
{
    public required GitDiffLineKind Kind { get; init; }

    public required string Text { get; init; }

    public int? OldLine { get; init; }

    public int? NewLine { get; init; }
}

/// <summary>
/// Indicates the kind of diff line.
/// </summary>
public enum GitDiffLineKind
{
    Context,
    Added,
    Removed,
    NoNewline
}
