using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace XamlVisualEditor.FileExplorerExtension.ViewModels;

public sealed class OpenFolderDialogViewModel : ReactiveObject
{
    public OpenFolderDialogViewModel(string? initialPath = null)
    {
        FolderPath = initialPath;

        BrowseCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? path = await SelectFolderInteraction.Handle(Unit.Default).FirstAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                FolderPath = path;
            }
        });

        OpenCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                return;
            }

            await CloseInteraction.Handle(FolderPath).FirstAsync();
        });

        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(null).FirstAsync());
    }

    [Reactive]
    public string? FolderPath { get; set; }

    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public Interaction<Unit, string?> SelectFolderInteraction { get; } = new();

    public Interaction<string?, Unit> CloseInteraction { get; } = new();
}
