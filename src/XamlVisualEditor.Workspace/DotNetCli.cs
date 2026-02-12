using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Workspace;

public sealed class DotNetCliResult
{
    public DotNetCliResult(int exitCode, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }

    public bool Success => ExitCode == 0;
}

public interface IDotNetCli
{
    Task<DotNetCliResult> RunAsync(IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct = default);
}

public sealed class DotNetCliRunner : IDotNetCli
{
    public async Task<DotNetCliResult> RunAsync(IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct = default)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using Process process = new() { StartInfo = startInfo };
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            using CancellationTokenRegistration registration = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
            });

            await process.WaitForExitAsync(ct);

            string standardOutput = await outputTask;
            string standardError = await errorTask;
            return new DotNetCliResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Exception ex)
        {
            return new DotNetCliResult(-1, string.Empty, ex.Message);
        }
    }
}
