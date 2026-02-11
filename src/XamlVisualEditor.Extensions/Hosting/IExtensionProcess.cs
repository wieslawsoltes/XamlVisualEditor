namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Represents an isolated extension process.</summary>
public interface IExtensionProcess : IAsyncDisposable
{
    /// <summary>Gets the extension id.</summary>
    string ExtensionId { get; }

    /// <summary>Gets whether the process is running.</summary>
    bool IsRunning { get; }

    /// <summary>Starts the extension process.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops the extension process.</summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Raised when the process exits.</summary>
    event EventHandler<ExtensionProcessExitedEventArgs>? Exited;
}

/// <summary>Provides exit details for an extension process.</summary>
public sealed class ExtensionProcessExitedEventArgs : EventArgs
{
    /// <summary>Creates exit args.</summary>
    public ExtensionProcessExitedEventArgs(int exitCode, bool isCrash, string? errorOutputTail)
    {
        ExitCode = exitCode;
        IsCrash = isCrash;
        ErrorOutputTail = errorOutputTail;
    }

    /// <summary>Gets the exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Gets whether the exit was a crash.</summary>
    public bool IsCrash { get; }

    /// <summary>Gets a tail of stderr output.</summary>
    public string? ErrorOutputTail { get; }
}

/// <summary>Creates extension processes.</summary>
public interface IExtensionProcessFactory
{
    /// <summary>Creates a process for an extension.</summary>
    IExtensionProcess Create(string extensionId);
}
