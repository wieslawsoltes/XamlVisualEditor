using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReactiveUI;

namespace XamlVisualEditor.OutputExtension.Views;

public sealed partial class OutputPanelView : UserControl
{
    private IDisposable? _clipboardHandler;

    public OutputPanelView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => RegisterHandlers();
        DetachedFromVisualTree += (_, _) => DisposeHandlers();
    }

    private void RegisterHandlers()
    {
        DisposeHandlers();

        if (DataContext is not OutputPanelViewModel vm)
        {
            return;
        }

        _clipboardHandler = vm.CopyToClipboardInteraction.RegisterHandler(async interaction =>
        {
            IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(interaction.Input);
            }

            interaction.SetOutput(Unit.Default);
        });
    }

    private void DisposeHandlers()
    {
        _clipboardHandler?.Dispose();
        _clipboardHandler = null;
    }
}
