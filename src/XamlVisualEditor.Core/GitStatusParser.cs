using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Core;

/// <summary>
/// Parses git status output into structured status models.
/// </summary>
public static class GitStatusParser
{
    /// <summary>
    /// Parses a porcelain v1 status output into a repository status snapshot.
    /// </summary>
    /// <param name="repoPath">The repository root used for the status call.</param>
    /// <param name="statusOutput">The output of `git status --porcelain=v1 -b`.</param>
    public static GitRepositoryStatus ParseStatus(string repoPath, string statusOutput)
    {
        string normalized = NormalizeLineEndings(statusOutput);
        string[] lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string branchName = string.Empty;
        string? upstreamName = null;
        int aheadBy = 0;
        int behindBy = 0;

        List<GitFileChange> changes = new();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                ParseBranchLine(line, ref branchName, ref upstreamName, ref aheadBy, ref behindBy);
                continue;
            }

            GitFileChange? change = ParseStatusLine(line);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return new GitRepositoryStatus
        {
            RepositoryRoot = repoPath,
            BranchName = branchName,
            UpstreamName = upstreamName,
            AheadBy = aheadBy,
            BehindBy = behindBy,
            Changes = changes
        };
    }

    private static string NormalizeLineEndings(string input)
    {
        return input.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static void ParseBranchLine(
        string line,
        ref string branchName,
        ref string? upstreamName,
        ref int aheadBy,
        ref int behindBy)
    {
        string content = line.Substring(3);
        int bracketIndex = content.IndexOf('[');
        string branchPart = bracketIndex >= 0 ? content.Substring(0, bracketIndex).Trim() : content.Trim();

        string? upstream = null;
        string branch = branchPart;
        int upstreamIndex = branchPart.IndexOf("...", StringComparison.Ordinal);
        if (upstreamIndex >= 0)
        {
            branch = branchPart.Substring(0, upstreamIndex);
            upstream = branchPart.Substring(upstreamIndex + 3);
        }

        branchName = branch;
        upstreamName = upstream;
        aheadBy = 0;
        behindBy = 0;

        if (bracketIndex >= 0)
        {
            int endIndex = content.IndexOf(']', bracketIndex);
            if (endIndex > bracketIndex)
            {
                string stats = content.Substring(bracketIndex + 1, endIndex - bracketIndex - 1);
                ParseAheadBehind(stats, ref aheadBy, ref behindBy);
            }
        }
    }

    private static void ParseAheadBehind(string stats, ref int aheadBy, ref int behindBy)
    {
        string[] parts = stats.Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (part.StartsWith("ahead ", StringComparison.Ordinal)
                && int.TryParse(part.Substring(6), out int ahead))
            {
                aheadBy = ahead;
            }
            else if (part.StartsWith("behind ", StringComparison.Ordinal)
                && int.TryParse(part.Substring(7), out int behind))
            {
                behindBy = behind;
            }
        }
    }

    private static GitFileChange? ParseStatusLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length < 3)
        {
            return null;
        }

        string status = line.Substring(0, 2);
        string pathPart = line.Length > 3 ? line.Substring(3) : string.Empty;
        string? oldPath = null;
        string path = pathPart;

        int arrowIndex = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
        if (arrowIndex >= 0)
        {
            oldPath = pathPart.Substring(0, arrowIndex);
            path = pathPart.Substring(arrowIndex + 4);
        }

        GitChangeKind indexStatus = MapStatusChar(status[0]);
        GitChangeKind workTreeStatus = MapStatusChar(status[1]);
        bool isUntracked = status == "??";
        bool isIgnored = status == "!!";

        return new GitFileChange
        {
            Path = path,
            OldPath = oldPath,
            IndexStatus = indexStatus,
            WorkTreeStatus = workTreeStatus,
            IsUntracked = isUntracked,
            IsIgnored = isIgnored,
            IsRenamed = indexStatus == GitChangeKind.Renamed || workTreeStatus == GitChangeKind.Renamed,
            IsCopied = indexStatus == GitChangeKind.Copied || workTreeStatus == GitChangeKind.Copied
        };
    }

    private static GitChangeKind MapStatusChar(char status)
    {
        return status switch
        {
            ' ' => GitChangeKind.None,
            'M' => GitChangeKind.Modified,
            'A' => GitChangeKind.Added,
            'D' => GitChangeKind.Deleted,
            'R' => GitChangeKind.Renamed,
            'C' => GitChangeKind.Copied,
            'U' => GitChangeKind.Unmerged,
            'T' => GitChangeKind.TypeChanged,
            '?' => GitChangeKind.Untracked,
            '!' => GitChangeKind.Ignored,
            _ => GitChangeKind.Unknown
        };
    }
}
