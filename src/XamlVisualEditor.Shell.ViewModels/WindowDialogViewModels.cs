using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>
/// ViewModel for a simple input dialog.
/// </summary>
public sealed class InputBoxDialogViewModel : ReactiveObject
{
    public string Title { get; }
    public string Prompt { get; }

    [Reactive]
    public string Value { get; set; }

    public Interaction<string?, Unit> CloseInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public InputBoxDialogViewModel(InputBoxOptions options)
    {
        Title = string.IsNullOrWhiteSpace(options.Title) ? "Input" : options.Title;
        Prompt = options.Prompt ?? string.Empty;
        Value = options.Value ?? string.Empty;

        ConfirmCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(Value));
        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(null));
    }
}

/// <summary>
/// ViewModel for a quick pick dialog.
/// </summary>
public sealed class QuickPickDialogViewModel : ReactiveObject
{
    public string Title { get; }

    public ObservableCollection<QuickPickItemViewModel> Items { get; } = new();

    [Reactive]
    public QuickPickItemViewModel? SelectedItem { get; set; }

    public Interaction<QuickPickItem?, Unit> CloseInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public QuickPickDialogViewModel(string? title, IReadOnlyList<QuickPickItem> items)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Select" : title;

        foreach (QuickPickItem item in items)
        {
            Items.Add(new QuickPickItemViewModel(item));
        }

        SelectedItem = Items.FirstOrDefault();

        IObservable<bool> canConfirm = this.WhenAnyValue(x => x.SelectedItem)
            .Select(item => item is not null);

        ConfirmCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(SelectedItem?.Item), canConfirm);
        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(null));
    }
}

/// <summary>
/// ViewModel for a single quick pick item.
/// </summary>
public sealed class QuickPickItemViewModel
{
    public QuickPickItemViewModel(QuickPickItem item)
    {
        Item = item;
    }

    public QuickPickItem Item { get; }

    public string Label => Item.Label;

    public string? Description => Item.Description;

    public string? Detail => Item.Detail;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Item.Description);

    public bool HasDetail => !string.IsNullOrWhiteSpace(Item.Detail);
}

/// <summary>
/// ViewModel for a simple message dialog.
/// </summary>
public sealed class MessageDialogViewModel : ReactiveObject
{
    public string Title { get; }
    public string Message { get; }

    public Interaction<bool, Unit> CloseInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public MessageDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;

        ConfirmCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(true));
    }
}
