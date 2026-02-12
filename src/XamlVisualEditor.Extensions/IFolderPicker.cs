namespace XamlVisualEditor.Extensions;

/// <summary>Provides folder picker dialogs.</summary>
public interface IFolderPicker
{
    /// <summary>Shows a folder picker and returns the selected path.</summary>
    Task<string?> PickFolderAsync(string? title, CancellationToken cancellationToken);
}