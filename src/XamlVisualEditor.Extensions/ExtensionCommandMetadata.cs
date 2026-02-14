namespace XamlVisualEditor.Extensions;

/// <summary>Describes command metadata used by host UI surfaces.</summary>
public sealed record CommandMetadata(
    string Title,
    string? Category = null,
    string? Icon = null,
    string? When = null,
    string? Keybinding = null,
    string? MacKeybinding = null,
    int Priority = 0);

/// <summary>Stores extension command metadata for host composition.</summary>
public interface ICommandMetadataRegistry
{
    /// <summary>Raised when metadata changes.</summary>
    event EventHandler? Changed;

    /// <summary>Registers metadata for a command.</summary>
    IDisposable Register(string commandId, CommandMetadata metadata);

    /// <summary>Gets metadata for a command, if available.</summary>
    bool TryGet(string commandId, out CommandMetadata metadata);

    /// <summary>Gets all registered metadata.</summary>
    IReadOnlyDictionary<string, CommandMetadata> GetAll();
}
