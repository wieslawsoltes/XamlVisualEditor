using System.Diagnostics;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;

namespace XamlVisualEditor.Extensions.Hosting.VscodeCompat;

/// <summary>Launches the VS Code compatibility node host.</summary>
public sealed class VscodeCompatHost : IAsyncDisposable
{
    private readonly ICommands _commands;
    private readonly IWindow _window;
    private readonly ISettings _settings;
    private readonly IExtensionLogger _logger;
    private readonly VscodeCompatExtensionLocator _locator = new();
    private readonly object _gate = new();
    private Process? _process;
    private VscodeCompatSession? _session;
    private CancellationTokenSource? _cts;
    private Task? _stderrTask;

    /// <summary>Creates a compatibility host.</summary>
    public VscodeCompatHost(
        ICommands commands,
        IWindow window,
        ISettings settings,
        IExtensionLogger logger)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Gets whether the host is running.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _process is not null && !_process.HasExited;
            }
        }
    }

    /// <summary>Starts the host process.</summary>
    public async Task StartAsync(VscodeCompatSettings settings, CancellationToken ct)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        await StopAsync().ConfigureAwait(false);

        if (!settings.Enabled)
        {
            return;
        }

        string? hostScript = VscodeCompatHostPaths.LocateHostScriptPath();
        if (string.IsNullOrWhiteSpace(hostScript))
        {
            _logger.Warn("VS Code compat host script not found.");
            return;
        }

        string extensionsRoot = string.IsNullOrWhiteSpace(settings.ExtensionsRoot)
            ? VscodeCompatHostPaths.GetDefaultExtensionsRoot()
            : settings.ExtensionsRoot;

        IReadOnlyList<string> extensions = _locator.ResolveExtensions(extensionsRoot, settings.ExtensionIds);
        if (extensions.Count == 0)
        {
            _logger.Warn("No VS Code extensions matched the configured ids.");
            return;
        }

        string nodePath = string.IsNullOrWhiteSpace(settings.NodePath) ? "node" : settings.NodePath;
        string args = BuildArgs(hostScript, extensions);

        var startInfo = new ProcessStartInfo
        {
            FileName = nodePath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(hostScript) ?? AppContext.BaseDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        string? nodePathEnv = VscodeCompatHostPaths.GetNodeModulePath(hostScript);
        if (!string.IsNullOrWhiteSpace(nodePathEnv))
        {
            startInfo.Environment["NODE_PATH"] = nodePathEnv;
        }

        Process? process = Process.Start(startInfo);
        if (process is null)
        {
            _logger.Error("Failed to start VS Code compat host process.");
            return;
        }

        var connection = new IdeBridgeJsonRpcConnection(
            process.StandardOutput.BaseStream,
            process.StandardInput.BaseStream);

        var session = new VscodeCompatSession(connection, _commands, _window, _settings, _logger);
        session.Start(ct);

        lock (_gate)
        {
            _process = process;
            _session = session;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _stderrTask = Task.Run(() => DrainStderrAsync(process, _cts.Token), _cts.Token);
        }

        _logger.Info("VS Code compat host started.");
    }

    /// <summary>Stops the host process.</summary>
    public async Task StopAsync()
    {
        Process? process;
        VscodeCompatSession? session;
        CancellationTokenSource? cts;
        Task? stderrTask;

        lock (_gate)
        {
            process = _process;
            session = _session;
            cts = _cts;
            stderrTask = _stderrTask;
            _process = null;
            _session = null;
            _cts = null;
            _stderrTask = null;
        }

        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        if (process is not null)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            process.Dispose();
        }

        if (stderrTask is not null)
        {
            try
            {
                await stderrTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task DrainStderrAsync(Process process, CancellationToken ct)
    {
        using StreamReader reader = process.StandardError;
        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length > 0)
            {
                _logger.Warn("VS Code host: " + line);
            }
        }
    }

    private static string BuildArgs(string hostScript, IReadOnlyList<string> extensions)
    {
        var builder = new List<string> { Quote(hostScript) };
        foreach (string extension in extensions)
        {
            builder.Add("--extension");
            builder.Add(Quote(extension));
        }

        return string.Join(' ', builder);
    }

    private static string Quote(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? '"' + value + '"' : value;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync());
    }
}
