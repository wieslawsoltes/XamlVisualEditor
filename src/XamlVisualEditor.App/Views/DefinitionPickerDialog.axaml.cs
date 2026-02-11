using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public sealed partial class DefinitionPickerDialog : Window
{
    private IDisposable? _closeHandler;

    public DefinitionPickerDialog()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => RegisterCloseHandler();
        Closed += (_, _) => _closeHandler?.Dispose();
    }

    private void RegisterCloseHandler()
    {
        _closeHandler?.Dispose();
        if (DataContext is DefinitionPickerDialogViewModel vm)
        {
            _closeHandler = vm.CloseInteraction.RegisterHandler(ctx =>
            {
                Close(ctx.Input);
                ctx.SetOutput(Unit.Default);
            });
        }
    }
}
