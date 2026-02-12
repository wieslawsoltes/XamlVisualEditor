using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using XamlVisualEditor.DotNetTemplatesExtension.ViewModels;

namespace XamlVisualEditor.DotNetTemplatesExtension.Views;

public sealed partial class WorkspaceOpenPromptDialog : Window
{
    private IDisposable? _closeHandler;

    public WorkspaceOpenPromptDialog()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => RegisterCloseHandler();
        Closed += (_, _) => _closeHandler?.Dispose();
    }

    private void RegisterCloseHandler()
    {
        _closeHandler?.Dispose();
        if (DataContext is WorkspaceOpenPromptDialogViewModel vm)
        {
            _closeHandler = vm.CloseInteraction.RegisterHandler(ctx =>
            {
                Close(ctx.Input);
                ctx.SetOutput(Unit.Default);
            });
        }
    }
}
