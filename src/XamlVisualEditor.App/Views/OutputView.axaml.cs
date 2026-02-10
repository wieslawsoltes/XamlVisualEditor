using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using ReactiveUI;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the output view.
/// </summary>
public sealed partial class OutputView : UserControl
{
    private IDisposable? _clipboardHandler;

    public OutputView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RegisterClipboardHandler();
        DetachedFromVisualTree += (_, _) => _clipboardHandler?.Dispose();
    }

    private void RegisterClipboardHandler()
    {
        _clipboardHandler?.Dispose();
        if (DataContext is OutputViewModel vm)
        {
            _clipboardHandler = vm.CopyToClipboardInteraction.RegisterHandler(async ctx =>
            {
                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                {
                    await clipboard.SetTextAsync(ctx.Input);
                }
                ctx.SetOutput(Unit.Default);
            });
        }
    }
}
