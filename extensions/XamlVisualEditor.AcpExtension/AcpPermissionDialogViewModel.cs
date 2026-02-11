using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using ReactiveUI;
using XamlVisualEditor.Acp;

namespace XamlVisualEditor.AcpExtension;

/// <summary>
/// ViewModel for ACP permission prompt dialog.
/// </summary>
public sealed class AcpPermissionDialogViewModel : ReactiveObject
{
    public string Title { get; } = "Permission Required";
    public string Message { get; }
    public string SessionId { get; }
    public string? ToolTitle { get; }
    public string? ToolKind { get; }
    public string? ToolCallId { get; }

    public ObservableCollection<AcpPermissionOptionViewModel> Options { get; } = new();

    public Interaction<AcpPermissionOutcome, Unit> CloseInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public AcpPermissionDialogViewModel(AcpPermissionRequest request)
    {
        SessionId = request.SessionId;
        ToolTitle = request.ToolTitle;
        ToolKind = request.ToolKind;
        ToolCallId = request.ToolCallId;

        string toolLabel = !string.IsNullOrWhiteSpace(ToolTitle) ? ToolTitle : "an operation";
        string kindLabel = !string.IsNullOrWhiteSpace(ToolKind) ? ToolKind : "tool";
        Message = $"The agent requests permission to run {kindLabel}: {toolLabel}.";

        foreach (AcpPermissionOption option in request.Options)
        {
            ReactiveCommand<Unit, Unit> selectCommand = ReactiveCommand.CreateFromTask(async () =>
                await CloseInteraction.Handle(AcpPermissionOutcome.Selected(option.OptionId)).ToTask().ConfigureAwait(false));
            Options.Add(new AcpPermissionOptionViewModel(option, selectCommand));
        }

        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(AcpPermissionOutcome.Cancelled()).ToTask().ConfigureAwait(false));
    }
}

public sealed class AcpPermissionOptionViewModel : ReactiveObject
{
    public string OptionId { get; }
    public string Name { get; }
    public string Kind { get; }
    public ReactiveCommand<Unit, Unit> SelectCommand { get; }

    public AcpPermissionOptionViewModel(AcpPermissionOption option, ReactiveCommand<Unit, Unit> selectCommand)
    {
        OptionId = option.OptionId;
        Name = option.Name;
        Kind = option.Kind;
        SelectCommand = selectCommand;
    }
}
