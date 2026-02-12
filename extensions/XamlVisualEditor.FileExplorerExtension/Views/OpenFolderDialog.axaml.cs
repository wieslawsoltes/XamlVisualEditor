using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ReactiveUI;
using XamlVisualEditor.FileExplorerExtension.ViewModels;

namespace XamlVisualEditor.FileExplorerExtension.Views;

public sealed partial class OpenFolderDialog : Window
{
    private IDisposable? _closeHandler;
    private IDisposable? _folderHandler;

    public OpenFolderDialog()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => RegisterHandlers();
        Closed += (_, _) =>
        {
            _closeHandler?.Dispose();
            _folderHandler?.Dispose();
        };
    }

    private void RegisterHandlers()
    {
        _closeHandler?.Dispose();
        _folderHandler?.Dispose();

        if (DataContext is OpenFolderDialogViewModel vm)
        {
            _closeHandler = vm.CloseInteraction.RegisterHandler(ctx =>
            {
                Close(ctx.Input);
                ctx.SetOutput(Unit.Default);
            });

            _folderHandler = vm.SelectFolderInteraction.RegisterHandler(async ctx =>
            {
                FolderPickerOpenOptions options = new()
                {
                    Title = "Select Folder",
                    AllowMultiple = false
                };

                var folders = await StorageProvider.OpenFolderPickerAsync(options);
                if (folders.Count > 0)
                {
                    ctx.SetOutput(folders[0].Path.LocalPath);
                }
                else
                {
                    ctx.SetOutput(null);
                }
            });
        }
    }
}
