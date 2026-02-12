using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ReactiveUI;
using XamlVisualEditor.DotNetTemplatesExtension.ViewModels;

namespace XamlVisualEditor.DotNetTemplatesExtension.Views;

public sealed partial class NewDotNetWizardDialog : Window
{
    private IDisposable? _closeHandler;
    private IDisposable? _folderHandler;

    public NewDotNetWizardDialog()
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

        if (DataContext is DotNetTemplateWizardViewModel vm)
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
                    Title = "Select Location",
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
