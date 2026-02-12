using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Services;

public sealed class AppWindow : IWindow
{
    private readonly MainWindowProvider _windowProvider;

    public AppWindow(MainWindowProvider windowProvider)
    {
        _windowProvider = windowProvider;
    }

    public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken)
    {
        return ShowMessageAsync("Information", message, cancellationToken);
    }

    public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken)
    {
        return ShowMessageAsync("Warning", message, cancellationToken);
    }

    public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken)
    {
        return ShowMessageAsync("Error", message, cancellationToken);
    }

    public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken)
    {
        InputBoxDialogViewModel viewModel = new(options);
        InputBoxDialog dialog = new()
        {
            DataContext = viewModel
        };

        return ShowDialogAsync<string?>(dialog, cancellationToken);
    }

    public Task<QuickPickItem?> ShowQuickPickAsync(
        IReadOnlyList<QuickPickItem> items,
        QuickPickOptions options,
        CancellationToken cancellationToken)
    {
        QuickPickDialogViewModel viewModel = new(options.Title, items);
        QuickPickDialog dialog = new()
        {
            DataContext = viewModel
        };

        return ShowDialogAsync<QuickPickItem?>(dialog, cancellationToken);
    }

    public IOutputChannel CreateOutputChannel(string name)
    {
        return new InMemoryOutputChannel(name);
    }

    public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority)
    {
        return new InMemoryStatusBarItem();
    }

    private Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken)
    {
        MessageDialogViewModel viewModel = new(title, message);
        MessageDialog dialog = new()
        {
            DataContext = viewModel
        };

        return ShowDialogAsync<bool>(dialog, cancellationToken);
    }

    private async Task<T?> ShowDialogAsync<T>(Window dialog, CancellationToken cancellationToken)
    {
        Window? owner = _windowProvider.MainWindow;
        Task<T?>? dialogTask = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (owner is not null)
            {
                dialogTask = dialog.ShowDialog<T?>(owner);
            }
            else
            {
                dialog.Show();
                dialogTask = Task.FromResult<T?>(default);
            }
        });

        if (dialogTask is null)
        {
            return default;
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            Dispatcher.UIThread.Post(() => dialog.Close());
        });

        return await dialogTask.ConfigureAwait(false);
    }
}
