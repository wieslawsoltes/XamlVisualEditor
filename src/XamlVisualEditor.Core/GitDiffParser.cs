using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Core;

/// <summary>
/// Parses unified diff text into structured git diff models.
/// </summary>
public static class GitDiffParser
{
    public static GitDiff ParseUnifiedDiff(string? diffText)
    {
        if (string.IsNullOrWhiteSpace(diffText))
        {
            return new GitDiff
            {
                Files = Array.Empty<GitDiffFile>(),
                RawText = diffText
            };
        }

        string[] lines = diffText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');

        List<GitDiffFile> files = new();
        GitDiffFileBuilder? currentFile = null;
        GitDiffHunkBuilder? currentHunk = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0 && i == lines.Length - 1)
            {
                break;
            }

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                FinalizeHunk(currentFile, currentHunk);
                currentHunk = null;
                FinalizeFile(files, currentFile);

                currentFile = new GitDiffFileBuilder
                {
                    HeaderLines = new List<string> { line }
                };

                ParseDiffHeader(line, currentFile);
                continue;
            }

            if (currentFile is null)
            {
                continue;
            }

            if (line.StartsWith("Binary files ", StringComparison.Ordinal)
                || line.StartsWith("GIT binary patch", StringComparison.Ordinal))
            {
                currentFile.IsBinary = true;
                currentFile.HeaderLines.Add(line);
                continue;
            }

            if (line.StartsWith("index ", StringComparison.Ordinal)
                || line.StartsWith("new file mode ", StringComparison.Ordinal)
                || line.StartsWith("deleted file mode ", StringComparison.Ordinal)
                || line.StartsWith("similarity index ", StringComparison.Ordinal)
                || line.StartsWith("rename from ", StringComparison.Ordinal)
                || line.StartsWith("rename to ", StringComparison.Ordinal))
            {
                currentFile.HeaderLines.Add(line);
                ParseRenameHeaders(line, currentFile);
                continue;
            }

            if (line.StartsWith("--- ", StringComparison.Ordinal)
                || line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                currentFile.HeaderLines.Add(line);
                ParseFilePathHeader(line, currentFile);
                continue;
            }

            if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                FinalizeHunk(currentFile, currentHunk);
                currentHunk = new GitDiffHunkBuilder();
                if (TryParseHunkHeader(line, currentHunk))
                {
                    currentHunk.Header = line;
                }
                else
                {
                    currentHunk.Header = line;
                    currentHunk.OldStart = 0;
                    currentHunk.NewStart = 0;
                }

                continue;
            }

            if (currentHunk is not null)
            {
                AppendHunkLine(currentHunk, line);
            }
        }

        FinalizeHunk(currentFile, currentHunk);
        FinalizeFile(files, currentFile);

        return new GitDiff
        {
            Files = files,
            RawText = diffText
        };
    }

    private static void FinalizeFile(List<GitDiffFile> files, GitDiffFileBuilder? builder)
    {
        if (builder is null)
        {
            return;
        }

        files.Add(builder.Build());
    }

    private static void FinalizeHunk(GitDiffFileBuilder? file, GitDiffHunkBuilder? hunk)
    {
        if (file is null || hunk is null)
        {
            return;
        }

        file.Hunks.Add(hunk.Build());
    }

    private static void ParseDiffHeader(string line, GitDiffFileBuilder builder)
    {
        string remainder = line.Substring("diff --git ".Length);
        int split = remainder.IndexOf(' ');
        if (split <= 0)
        {
            return;
        }

        string oldPath = remainder.Substring(0, split);
        string newPath = remainder.Substring(split + 1);

        builder.OldPath = NormalizePath(oldPath);
        builder.Path = NormalizePath(newPath);
    }

    private static void ParseFilePathHeader(string line, GitDiffFileBuilder builder)
    {
        string value = line.Substring(4).Trim();
        if (value.StartsWith("a/", StringComparison.Ordinal))
        {
            builder.OldPath = value.Substring(2);
        }
        else if (value.StartsWith("b/", StringComparison.Ordinal))
        {
            builder.Path = value.Substring(2);
        }
        else if (string.Equals(value, "/dev/null", StringComparison.Ordinal))
        {
            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                builder.OldPath = null;
            }
        }
    }

    private static void ParseRenameHeaders(string line, GitDiffFileBuilder builder)
    {
        if (line.StartsWith("rename from ", StringComparison.Ordinal))
        {
            builder.OldPath = line.Substring("rename from ".Length);
        }
        else if (line.StartsWith("rename to ", StringComparison.Ordinal))
        {
            builder.Path = line.Substring("rename to ".Length);
        }
    }

    private static bool TryParseHunkHeader(string header, GitDiffHunkBuilder builder)
    {
        int start = header.IndexOf("@@", StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        int i = start + 2;
        i = SkipSpaces(header, i);
        if (i >= header.Length || header[i] != '-')
        {
            return false;
        }

        i++;
        if (!TryReadNumber(header, ref i, out int oldStart))
        {
            return false;
        }

        int oldCount = 1;
        if (i < header.Length && header[i] == ',')
        {
            i++;
            if (!TryReadNumber(header, ref i, out oldCount))
            {
                return false;
            }
        }

        i = SkipSpaces(header, i);
        if (i >= header.Length || header[i] != '+')
        {
            return false;
        }

        i++;
        if (!TryReadNumber(header, ref i, out int newStart))
        {
            return false;
        }

        int newCount = 1;
        if (i < header.Length && header[i] == ',')
        {
            i++;
            if (!TryReadNumber(header, ref i, out newCount))
            {
                return false;
            }
        }

        builder.OldStart = oldStart;
        builder.OldCount = oldCount;
        builder.NewStart = newStart;
        builder.NewCount = newCount;
        return true;
    }

    private static void AppendHunkLine(GitDiffHunkBuilder hunk, string line)
    {
        if (line.StartsWith("\\ No newline", StringComparison.Ordinal))
        {
            hunk.Lines.Add(new GitDiffLine
            {
                Kind = GitDiffLineKind.NoNewline,
                Text = line,
                OldLine = null,
                NewLine = null
            });
            return;
        }

        if (line.Length == 0)
        {
            hunk.AddContext(string.Empty);
            return;
        }

        char prefix = line[0];
        string text = line.Substring(1);

        switch (prefix)
        {
            case '+':
                hunk.AddAdded(text);
                break;
            case '-':
                hunk.AddRemoved(text);
                break;
            case ' ':
                hunk.AddContext(text);
                break;
            default:
                hunk.AddContext(line);
                break;
        }
    }

    private static int SkipSpaces(string text, int index)
    {
        while (index < text.Length && text[index] == ' ')
        {
            index++;
        }

        return index;
    }

    private static bool TryReadNumber(string text, ref int index, out int value)
    {
        value = 0;
        int start = index;
        while (index < text.Length && char.IsDigit(text[index]))
        {
            value = (value * 10) + (text[index] - '0');
            index++;
        }

        return index > start;
    }

    private static string NormalizePath(string path)
    {
        if (path.StartsWith("a/", StringComparison.Ordinal)
            || path.StartsWith("b/", StringComparison.Ordinal))
        {
            return path.Substring(2);
        }

        return path;
    }

    private sealed class GitDiffFileBuilder
    {
        public string Path { get; set; } = string.Empty;

        public string? OldPath { get; set; }

        public bool IsBinary { get; set; }

        public List<string> HeaderLines { get; init; } = new();

        public List<GitDiffHunk> Hunks { get; } = new();

        public GitDiffFile Build()
        {
            return new GitDiffFile
            {
                Path = Path,
                OldPath = OldPath,
                IsBinary = IsBinary,
                HeaderLines = HeaderLines,
                Hunks = Hunks
            };
        }
    }

    private sealed class GitDiffHunkBuilder
    {
        public string Header { get; set; } = string.Empty;

        public int OldStart { get; set; }

        public int OldCount { get; set; } = 1;

        public int NewStart { get; set; }

        public int NewCount { get; set; } = 1;

        public List<GitDiffLine> Lines { get; } = new();

        private int _oldLine;
        private int _newLine;
        private bool _hasLineCounters;

        public void AddContext(string text)
        {
            EnsureLineCounters();
            GitDiffLine line = new()
            {
                Kind = GitDiffLineKind.Context,
                Text = text,
                OldLine = _oldLine,
                NewLine = _newLine
            };
            Lines.Add(line);
            _oldLine++;
            _newLine++;
        }

        public void AddAdded(string text)
        {
            EnsureLineCounters();
            GitDiffLine line = new()
            {
                Kind = GitDiffLineKind.Added,
                Text = text,
                OldLine = null,
                NewLine = _newLine
            };
            Lines.Add(line);
            _newLine++;
        }

        public void AddRemoved(string text)
        {
            EnsureLineCounters();
            GitDiffLine line = new()
            {
                Kind = GitDiffLineKind.Removed,
                Text = text,
                OldLine = _oldLine,
                NewLine = null
            };
            Lines.Add(line);
            _oldLine++;
        }

        private void EnsureLineCounters()
        {
            if (_hasLineCounters)
            {
                return;
            }

            _oldLine = OldStart;
            _newLine = NewStart;
            _hasLineCounters = true;
        }

        public GitDiffHunk Build()
        {
            return new GitDiffHunk
            {
                Header = Header,
                OldStart = OldStart,
                OldCount = OldCount,
                NewStart = NewStart,
                NewCount = NewCount,
                Lines = Lines
            };
        }
    }
}
