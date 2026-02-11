namespace XamlVisualEditor.Extensions;

/// <summary>Provides user-facing UI services.</summary>
public interface IWindow
{
    /// <summary>Shows an information message.</summary>
    Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken);

    /// <summary>Shows a warning message.</summary>
    Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken);

    /// <summary>Shows an error message.</summary>
    Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken);

    /// <summary>Shows a single-value input box.</summary>
    Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken);

    /// <summary>Shows a quick pick selector.</summary>
    Task<QuickPickItem?> ShowQuickPickAsync(
        IReadOnlyList<QuickPickItem> items,
        QuickPickOptions options,
        CancellationToken cancellationToken);

    /// <summary>Creates an output channel.</summary>
    IOutputChannel CreateOutputChannel(string name);

    /// <summary>Creates a status bar item.</summary>
    IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority);
}

/// <summary>Options for the input box.</summary>
public sealed record InputBoxOptions(string? Title, string? Prompt, string? Value);

/// <summary>Options for the quick pick.</summary>
public sealed record QuickPickOptions(string? Title, bool CanPickMany);

/// <summary>Represents a quick pick item.</summary>
public sealed record QuickPickItem(string Label, string? Description, string? Detail);

/// <summary>Represents an output channel.</summary>
public interface IOutputChannel : IDisposable
{
    /// <summary>Gets the channel name.</summary>
    string Name { get; }

    /// <summary>Appends text without a newline.</summary>
    void Append(string value);

    /// <summary>Appends text with a newline.</summary>
    void AppendLine(string value);

    /// <summary>Shows the output channel.</summary>
    void Show();

    /// <summary>Hides the output channel.</summary>
    void Hide();

    /// <summary>Clears the output channel content.</summary>
    void Clear();
}

/// <summary>Alignment of status bar items.</summary>
public enum StatusBarAlignment
{
    /// <summary>Left aligned.</summary>
    Left,

    /// <summary>Right aligned.</summary>
    Right
}

/// <summary>Represents a status bar item.</summary>
public interface IStatusBarItem : IDisposable
{
    /// <summary>Gets or sets the text shown in the status bar.</summary>
    string Text { get; set; }

    /// <summary>Gets or sets the tooltip text.</summary>
    string? Tooltip { get; set; }

    /// <summary>Gets or sets the associated command id.</summary>
    string? CommandId { get; set; }

    /// <summary>Shows the status bar item.</summary>
    void Show();

    /// <summary>Hides the status bar item.</summary>
    void Hide();
}
