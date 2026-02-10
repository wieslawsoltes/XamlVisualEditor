using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed class AcpAgentHost : IAsyncDisposable
{
    private readonly Process _process;
    private readonly AcpProtocolClient _client;
    private readonly CancellationTokenSource _stderrCts;
    private Task? _stderrLoop;
    private readonly bool _ownsProcess;

    private AcpAgentHost(
        Process process,
        AcpProtocolClient client,
        CancellationTokenSource stderrCts,
        Task? stderrLoop,
        bool ownsProcess)
    {
        _process = process;
        _client = client;
        _stderrCts = stderrCts;
        _stderrLoop = stderrLoop;
        _ownsProcess = ownsProcess;
    }

    public AcpProtocolClient Client => _client;

    public event Action<string>? StderrReceived;

    public static async Task<AcpAgentHost> StartAsync(AcpAgentProcessOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.FileName))
        {
            throw new ArgumentException("Agent executable path is required.", nameof(options));
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = options.FileName,
            Arguments = options.Arguments,
            WorkingDirectory = options.WorkingDirectory ?? string.Empty,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = options.RedirectStandardError,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (KeyValuePair<string, string> pair in options.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start ACP agent process.");
        }

        Stream input = process.StandardOutput.BaseStream;
        Stream output = process.StandardInput.BaseStream;

        AcpMessageReader reader = new(input);
        AcpMessageWriter writer = new(output);
        AcpProtocolClient client = new(reader, writer);
        client.Start(ct);

        CancellationTokenSource stderrCts = new();
        AcpAgentHost host = new(process, client, stderrCts, null, ownsProcess: true);
        if (options.RedirectStandardError)
        {
            host._stderrLoop = Task.Run(() => host.PumpStderrAsync(process.StandardError, stderrCts.Token));
        }
        await Task.Yield();
        return host;
    }

    public static AcpAgentHost CreateForTests(Process process, AcpProtocolClient client)
    {
        CancellationTokenSource stderrCts = new();
        return new AcpAgentHost(process, client, stderrCts, null, ownsProcess: false);
    }

    private async Task PumpStderrAsync(StreamReader reader, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length > 0)
            {
                StderrReceived?.Invoke(line);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);

        _stderrCts.Cancel();
        if (_stderrLoop is not null)
        {
            try
            {
                await _stderrLoop.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        if (_ownsProcess && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        _process.Dispose();
        _stderrCts.Dispose();
    }
}
