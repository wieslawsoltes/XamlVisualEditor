using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace XamlVisualEditor.Lsp;

internal interface ILspTransport : IAsyncDisposable
{
    Stream Input { get; }
    Stream Output { get; }
}

internal sealed class ProcessLspTransport : ILspTransport
{
    private readonly Process _process;
    private readonly ILogger<ProcessLspTransport> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _stderrTask;
    private readonly object _stderrLock = new();
    private readonly Queue<string> _stderrBuffer = new();
    private const int MaxStderrLines = 25;

    public ProcessLspTransport(LspServerConfiguration configuration, ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<ProcessLspTransport>() ?? NullLogger<ProcessLspTransport>.Instance;
        ProcessStartInfo startInfo = new()
        {
            FileName = configuration.ServerPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (!string.IsNullOrWhiteSpace(configuration.WorkingDirectory))
        {
            startInfo.WorkingDirectory = configuration.WorkingDirectory;
        }

        foreach (string arg in configuration.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (KeyValuePair<string, string> pair in configuration.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _process.Exited += (_, _) => LogExitInfo();

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start LSP server process.");
        }

        Input = _process.StandardOutput.BaseStream;
        Output = _process.StandardInput.BaseStream;
        _stderrTask = Task.Run(() => PumpStandardErrorAsync(_process, _cts.Token));
    }

    public Stream Input { get; }
    public Stream Output { get; }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_stderrTask is not null)
        {
            try
            {
                await _stderrTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to stop LSP stderr pump.");
            }
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
        _cts.Dispose();
    }

    private async Task PumpStandardErrorAsync(Process process, CancellationToken ct)
    {
        using StreamReader reader = process.StandardError;
        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                lock (_stderrLock)
                {
                    if (_stderrBuffer.Count >= MaxStderrLines)
                    {
                        _stderrBuffer.Dequeue();
                    }

                    _stderrBuffer.Enqueue(line);
                }
                _logger.LogWarning("LSP server stderr: {Message}", line);
            }
        }
    }

    private void LogExitInfo()
    {
        try
        {
            int exitCode = _process.HasExited ? _process.ExitCode : -1;
            string stderrTail = GetStderrTail();
            if (string.IsNullOrWhiteSpace(stderrTail))
            {
                _logger.LogWarning("LSP server process exited with code {ExitCode}.", exitCode);
            }
            else
            {
                _logger.LogWarning("LSP server process exited with code {ExitCode}. Stderr tail: {Stderr}", exitCode, stderrTail);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to log LSP server exit info.");
        }
    }

    private string GetStderrTail()
    {
        lock (_stderrLock)
        {
            return _stderrBuffer.Count == 0
                ? string.Empty
                : string.Join(" | ", _stderrBuffer);
        }
    }
}

internal sealed class StreamLspTransport : ILspTransport
{
    public StreamLspTransport(Stream input, Stream output)
    {
        Input = input;
        Output = output;
    }

    public Stream Input { get; }
    public Stream Output { get; }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
