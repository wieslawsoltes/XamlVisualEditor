using System;
using System.Diagnostics;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using ReactiveUI;
using XamlVisualEditor.Acp;

namespace XamlVisualEditor.AcpExtension.Views;

public sealed partial class AcpToolView : UserControl
{
    private IDisposable? _clipboardHandler;
    private IDisposable? _openUrlHandler;
    private IDisposable? _permissionHandler;

    public AcpToolView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RegisterHandlers();
        DetachedFromVisualTree += (_, _) => DisposeHandlers();
    }

    private void RegisterHandlers()
    {
        DisposeHandlers();

        if (DataContext is not AcpToolViewModel vm)
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

        _openUrlHandler = vm.OpenUrlInteraction.RegisterHandler(interaction =>
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = interaction.Input,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch
            {
            }

            interaction.SetOutput(Unit.Default);
            return System.Threading.Tasks.Task.CompletedTask;
        });

        _permissionHandler = vm.PermissionInteraction.RegisterHandler(async interaction =>
        {
            AcpPermissionDialogViewModel dialogVm = new(interaction.Input);
            AcpPermissionDialog dialog = new()
            {
                DataContext = dialogVm
            };

            Window? owner = VisualRoot as Window;
            if (owner is null)
            {
                interaction.SetOutput(AcpPermissionOutcome.Cancelled());
                return;
            }

            AcpPermissionOutcome result = await dialog.ShowDialog<AcpPermissionOutcome>(owner);
            interaction.SetOutput(result);
        });
    }

    private void DisposeHandlers()
    {
        _clipboardHandler?.Dispose();
        _openUrlHandler?.Dispose();
        _permissionHandler?.Dispose();
        _clipboardHandler = null;
        _openUrlHandler = null;
        _permissionHandler = null;
    }
}
