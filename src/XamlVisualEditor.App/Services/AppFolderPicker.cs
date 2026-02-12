using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.App.Services;

public sealed class AppFolderPicker : IFolderPicker
{
    private readonly MainWindowProvider _windowProvider;

    public AppFolderPicker(MainWindowProvider windowProvider)
    {
        _windowProvider = windowProvider;
    }

    public async Task<string?> PickFolderAsync(string? title, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return await PickFolderCoreAsync(title, cancellationToken).ConfigureAwait(false);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => PickFolderCoreAsync(title, cancellationToken)).ConfigureAwait(false);
    }

    private async Task<string?> PickFolderCoreAsync(string? title, CancellationToken cancellationToken)
    {
        Window? owner = _windowProvider.MainWindow;
        if (owner?.StorageProvider is null)
        {
            return null;
        }

        FolderPickerOpenOptions options = new()
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Select Folder" : title,
            AllowMultiple = false
        };

        var results = await owner.StorageProvider.OpenFolderPickerAsync(options);
        if (results.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return results[0].Path.LocalPath;
    }
}