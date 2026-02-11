namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Supervises extension processes and restarts on crash.</summary>
public sealed class ExtensionHostSupervisor
{
    private readonly IExtensionProcessFactory _factory;
    private readonly IExtensionCrashReporter _crashReporter;
    private readonly ExtensionRestartPolicy _restartPolicy;
    private readonly Dictionary<string, List<DateTimeOffset>> _restartHistory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IExtensionProcess> _running = new(StringComparer.Ordinal);

    /// <summary>Creates a supervisor.</summary>
    public ExtensionHostSupervisor(
        IExtensionProcessFactory factory,
        IExtensionCrashReporter crashReporter,
        ExtensionRestartPolicy restartPolicy)
    {
        _factory = factory;
        _crashReporter = crashReporter;
        _restartPolicy = restartPolicy;
    }

    /// <summary>Raised when an extension crashes and will not be restarted.</summary>
    public event EventHandler<ExtensionCrashInfo>? ExtensionCrashed;

    /// <summary>Starts an extension process.</summary>
    public async Task StartAsync(string extensionId, CancellationToken cancellationToken)
    {
        if (_running.ContainsKey(extensionId))
        {
            return;
        }

        IExtensionProcess process = _factory.Create(extensionId);
        process.Exited += OnProcessExited;
        _running[extensionId] = process;
        await process.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops an extension process.</summary>
    public async Task StopAsync(string extensionId, CancellationToken cancellationToken)
    {
        if (!_running.TryGetValue(extensionId, out IExtensionProcess? process))
        {
            return;
        }

        process.Exited -= OnProcessExited;
        await process.StopAsync(cancellationToken).ConfigureAwait(false);
        await process.DisposeAsync().ConfigureAwait(false);
        _running.Remove(extensionId);
    }

    private async void OnProcessExited(object? sender, ExtensionProcessExitedEventArgs args)
    {
        if (sender is not IExtensionProcess process)
        {
            return;
        }

        process.Exited -= OnProcessExited;
        _running.Remove(process.ExtensionId);

        if (!args.IsCrash)
        {
            await process.DisposeAsync().ConfigureAwait(false);
            return;
        }

        var crashInfo = new ExtensionCrashInfo(
            process.ExtensionId,
            args.ExitCode,
            DateTimeOffset.UtcNow,
            args.ErrorOutputTail);

        await _crashReporter.RecordAsync(crashInfo, CancellationToken.None).ConfigureAwait(false);

        if (!ShouldRestart(process.ExtensionId))
        {
            ExtensionCrashed?.Invoke(this, crashInfo);
            await process.DisposeAsync().ConfigureAwait(false);
            return;
        }

        await process.DisposeAsync().ConfigureAwait(false);
        await RestartAsync(process.ExtensionId).ConfigureAwait(false);
    }

    private bool ShouldRestart(string extensionId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!_restartHistory.TryGetValue(extensionId, out List<DateTimeOffset>? history))
        {
            history = new List<DateTimeOffset>();
            _restartHistory[extensionId] = history;
        }

        history.RemoveAll(timestamp => now - timestamp > _restartPolicy.Window);
        if (history.Count >= _restartPolicy.MaxRestarts)
        {
            return false;
        }

        history.Add(now);
        return true;
    }

    private async Task RestartAsync(string extensionId)
    {
        IExtensionProcess process = _factory.Create(extensionId);
        process.Exited += OnProcessExited;
        _running[extensionId] = process;
        await process.StartAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
