namespace XamlVisualEditor.Extensions;

/// <summary>Registers and executes extension commands.</summary>
public interface ICommands
{
    /// <summary>Registers a command handler.</summary>
    IDisposable Register(string commandId, Func<CommandContext, Task> handler);

    /// <summary>Executes a command by id.</summary>
    Task ExecuteAsync(string commandId, IReadOnlyList<object?>? args, CancellationToken cancellationToken);

    /// <summary>Gets the list of known commands.</summary>
    Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken);
}

/// <summary>Provides context to a command handler.</summary>
public sealed class CommandContext
{
    /// <summary>Creates a command context.</summary>
    public CommandContext(IReadOnlyList<object?>? arguments, IWorkspace workspace, IWindow window)
    {
        Arguments = arguments;
        Workspace = workspace;
        Window = window;
    }

    /// <summary>Gets the command arguments.</summary>
    public IReadOnlyList<object?>? Arguments { get; }

    /// <summary>Gets the workspace service.</summary>
    public IWorkspace Workspace { get; }

    /// <summary>Gets the window service.</summary>
    public IWindow Window { get; }
}
