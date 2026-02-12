using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Debugging.Dap;

internal sealed class DapDebugAdapterHost : IAsyncDisposable
{
    private readonly Process _process;
    private readonly DapProtocolClient _client;

    private DapDebugAdapterHost(Process process, DapProtocolClient client)
    {
        _process = process;
        _client = client;
    }

    public DapProtocolClient Client => _client;

    public static async Task<DapDebugAdapterHost> StartAsync(string adapterPath, CancellationToken ct)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = adapterPath,
            Arguments = "--interpreter=vscode",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start debug adapter process.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        });

        Stream input = process.StandardOutput.BaseStream;
        Stream output = process.StandardInput.BaseStream;

        DapMessageReader reader = new(input);
        DapMessageWriter writer = new(output);
        DapProtocolClient client = new(reader, writer);
        client.Start(ct);

        DapDebugAdapterHost host = new(process, client);
        await Task.Yield();
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);

        if (!_process.HasExited)
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
    }
}
