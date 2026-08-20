using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(1);

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

            using CancellationTokenSource outputCancellation = new();
            Task<string> outputTask = ReadOutputAsync(process.StandardOutput, outputCancellation.Token);
            Task<string> errorTask = ReadOutputAsync(process.StandardError, outputCancellation.Token);

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

            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                await WaitForOutputAsync(outputTask, errorTask, outputCancellation).ConfigureAwait(false);

                string standardOutput = await outputTask.ConfigureAwait(false);
                string standardError = await errorTask.ConfigureAwait(false);
                return new DotNetCliResult(process.ExitCode, standardOutput, standardError);
            }
            finally
            {
                outputCancellation.Cancel();
                await ObserveOutputTasksAsync(outputTask, errorTask).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return new DotNetCliResult(-1, string.Empty, ex.Message);
        }
    }

    private static async Task<string> ReadOutputAsync(StreamReader reader, CancellationToken ct)
    {
        char[] buffer = new char[4096];
        StringBuilder output = new();

        try
        {
            while (true)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                output.Append(buffer, 0, read);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }

        return output.ToString();
    }

    private static async Task WaitForOutputAsync(
        Task<string> outputTask,
        Task<string> errorTask,
        CancellationTokenSource outputCancellation)
    {
        Task outputTasks = Task.WhenAll(outputTask, errorTask);
        try
        {
            await outputTasks.WaitAsync(OutputDrainTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            outputCancellation.Cancel();
            await outputTasks.ConfigureAwait(false);
        }
    }

    private static async Task ObserveOutputTasksAsync(Task<string> outputTask, Task<string> errorTask)
    {
        try
        {
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
