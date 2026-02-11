using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using ReactiveUI;

namespace XamlVisualEditor.McpExtension.Views;

public sealed partial class McpPanelView : UserControl
{
    private IDisposable? _clipboardHandler;

    public McpPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RegisterHandlers();
        DetachedFromVisualTree += (_, _) => DisposeHandlers();
    }

    private void RegisterHandlers()
    {
        DisposeHandlers();

        if (DataContext is not McpPanelViewModel vm)
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
