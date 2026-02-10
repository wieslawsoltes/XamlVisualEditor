using System;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core.Debugging;

namespace XamlVisualEditor.Debugging.Dap;

public sealed class DapDebuggerService : IDebuggerService
{
    public async Task<IDebugSession> LaunchAsync(DebugLaunchOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.AdapterPath))
        {
            throw new ArgumentException("Adapter path is required.", nameof(options));
        }

        DapDebugAdapterHost host = await DapDebugAdapterHost.StartAsync(options.AdapterPath, ct).ConfigureAwait(false);
        DapDebugSession session = new(host);
        await session.InitializeAsync(ct).ConfigureAwait(false);
        await session.LaunchAsync(options, ct).ConfigureAwait(false);
        return session;
    }

    public async Task<IDebugSession> AttachAsync(DebugAttachOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.AdapterPath))
        {
            throw new ArgumentException("Adapter path is required.", nameof(options));
        }

        DapDebugAdapterHost host = await DapDebugAdapterHost.StartAsync(options.AdapterPath, ct).ConfigureAwait(false);
        DapDebugSession session = new(host);
        await session.InitializeAsync(ct).ConfigureAwait(false);
        await session.AttachAsync(options, ct).ConfigureAwait(false);
        return session;
    }
}
