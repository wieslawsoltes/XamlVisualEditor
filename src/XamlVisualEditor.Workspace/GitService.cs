using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Workspace;

/// <summary>
/// Git CLI-backed service for repository status and diff operations.
/// </summary>
public sealed class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    public GitService(ILogger<GitService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitService>.Instance;
    }

    public async Task<string?> GetRepositoryRootAsync(string path, CancellationToken ct = default)
    {
        string? workingDirectory = ResolveWorkingDirectory(path);
        if (workingDirectory is null)
        {
            return null;
        }

        GitCommandResult result = await RunGitAsync(workingDirectory, new[] { "rev-parse", "--show-toplevel" }, ct);
        if (result.ExitCode != 0)
        {
            return null;
        }

        string output = result.StandardOutput.Trim();
        return string.IsNullOrWhiteSpace(output) ? null : output;
    }

    public async Task<GitRepositoryStatus> GetStatusAsync(string repoPath, CancellationToken ct = default)
    {
        GitCommandResult result = await RunGitAsync(repoPath, new[] { "status", "--porcelain=v1", "-b" }, ct);
        if (result.ExitCode != 0)
        {
            return new GitRepositoryStatus
            {
                RepositoryRoot = repoPath,
                IsRepository = false,
                ErrorMessage = result.StandardError,
                Changes = Array.Empty<GitFileChange>()
            };
        }

        return GitStatusParser.ParseStatus(repoPath, result.StandardOutput);
    }

    public async Task<GitDiff> GetDiffAsync(string repoPath, GitDiffRequest request, CancellationToken ct = default)
    {
        string[] args = BuildDiffArguments(request);
        GitCommandResult result = await RunGitAsync(repoPath, args, ct, allowNonZeroExitCode: true);
        if (result.ExitCode > 1)
        {
            _logger.LogWarning("git diff failed: {Error}", result.StandardError);
            return new GitDiff
            {
                Files = Array.Empty<GitDiffFile>(),
                RawText = result.StandardOutput
            };
        }

        return GitDiffParser.ParseUnifiedDiff(result.StandardOutput);
    }

    public async Task StageAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return;
        }

        List<string> args = new() { "add", "--" };
        args.AddRange(paths);
        await RunGitAsync(repoPath, args, ct);
    }

    public async Task UnstageAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return;
        }

        List<string> args = new() { "reset", "-q", "--" };
        args.AddRange(paths);
        await RunGitAsync(repoPath, args, ct);
    }

    public async Task DiscardAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return;
        }

        List<string> args = new() { "restore", "--worktree", "--staged", "--" };
        args.AddRange(paths);
        await RunGitAsync(repoPath, args, ct);
    }

    public async Task RemoveUntrackedAsync(string repoPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return;
        }

        List<string> args = new() { "clean", "-f", "--" };
        args.AddRange(paths);
        await RunGitAsync(repoPath, args, ct);
    }

    public async Task CommitAsync(string repoPath, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        List<string> args = new() { "commit", "-m", message };
        await RunGitAsync(repoPath, args, ct);
    }

    private static string? ResolveWorkingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        if (File.Exists(path))
        {
            return Path.GetDirectoryName(path);
        }

        return null;
    }

    private static string[] BuildDiffArguments(GitDiffRequest request)
    {
        List<string> args = new();

        if (request.IsUntracked && !string.IsNullOrWhiteSpace(request.Path))
        {
            args.Add("diff");
            args.Add("--no-index");
            args.Add("--no-color");
            args.Add("--unified=3");
            args.Add("--");
            args.Add("/dev/null");
            args.Add(request.Path!);
            return args.ToArray();
        }

        args.Add("diff");
        if (request.Staged)
        {
            args.Add("--cached");
        }

        args.Add("--no-color");
        args.Add("--unified=3");

        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            args.Add("--");
            args.Add(request.Path!);
        }

        return args.ToArray();
    }

    private async Task<GitCommandResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> args,
        CancellationToken ct,
        bool allowNonZeroExitCode = false)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();

        using CancellationTokenRegistration registration = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        });

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(ct);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(ct));

        GitCommandResult result = new()
        {
            ExitCode = process.ExitCode,
            StandardOutput = outputTask.Result,
            StandardError = errorTask.Result
        };

        if (!allowNonZeroExitCode && result.ExitCode != 0)
        {
            _logger.LogWarning("git command failed: {Args} ({ExitCode})", string.Join(" ", args), result.ExitCode);
        }

        return result;
    }

    private sealed class GitCommandResult
    {
        public int ExitCode { get; init; }

        public string StandardOutput { get; init; } = string.Empty;

        public string StandardError { get; init; } = string.Empty;
    }
}
