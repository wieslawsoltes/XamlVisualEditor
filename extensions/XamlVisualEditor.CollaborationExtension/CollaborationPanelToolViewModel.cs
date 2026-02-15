using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.CollaborationExtension;

public sealed record CollaborationParticipantItemViewModel(
    string Id,
    string DisplayName,
    string Color,
    bool IsLocal,
    string? CurrentFile,
    int CaretLine,
    int CaretColumn);

public sealed class CollaborationPanelToolViewModel : ReactiveObject, IDisposable
{
    private readonly ICollaborationHost _collaboration;
    private readonly IWindow _window;
    private readonly CompositeDisposable _disposables = new();
    private bool _isSessionActive;
    private string? _sessionId;
    private string _statusMessage = "Not connected";
    private string? _joinSessionId;
    private string? _invitee;

    public CollaborationPanelToolViewModel(ICollaborationHost collaboration, IWindow window)
    {
        _collaboration = collaboration;
        _window = window;

        IObservable<bool> canConnect = this.WhenAnyValue(x => x.IsSessionActive)
            .Select(active => !active);
        IObservable<bool> canJoin = this.WhenAnyValue(
            x => x.IsSessionActive,
            x => x.JoinSessionId,
            (active, sessionId) => !active && !string.IsNullOrWhiteSpace(sessionId));
        IObservable<bool> canLeaveOrShare = this.WhenAnyValue(x => x.IsSessionActive);
        IObservable<bool> canInvite = this.WhenAnyValue(
            x => x.IsSessionActive,
            x => x.Invitee,
            (active, invitee) => active && !string.IsNullOrWhiteSpace(invitee));

        StartSessionCommand = ReactiveCommand.CreateFromTask(StartSessionAsync, canConnect);
        JoinSessionCommand = ReactiveCommand.CreateFromTask(JoinSessionAsync, canJoin);
        LeaveSessionCommand = ReactiveCommand.CreateFromTask(LeaveSessionAsync, canLeaveOrShare);
        ShareSessionCommand = ReactiveCommand.CreateFromTask(ShareSessionAsync, canLeaveOrShare);
        InviteParticipantCommand = ReactiveCommand.CreateFromTask(InviteParticipantAsync, canInvite);
        _disposables.Add(StartSessionCommand);
        _disposables.Add(JoinSessionCommand);
        _disposables.Add(LeaveSessionCommand);
        _disposables.Add(ShareSessionCommand);
        _disposables.Add(InviteParticipantCommand);

        _collaboration.SessionChanged += OnSessionChanged;
        _collaboration.ParticipantsChanged += OnParticipantsChanged;

        ApplySessionState(_collaboration.IsSessionActive, _collaboration.SessionId, _collaboration.StatusMessage);
        ApplyParticipants(_collaboration.GetParticipants());
    }

    public bool IsSessionActive
    {
        get => _isSessionActive;
        private set => this.RaiseAndSetIfChanged(ref _isSessionActive, value);
    }

    public string? SessionId
    {
        get => _sessionId;
        private set => this.RaiseAndSetIfChanged(ref _sessionId, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string? JoinSessionId
    {
        get => _joinSessionId;
        set => this.RaiseAndSetIfChanged(ref _joinSessionId, value);
    }

    public string? Invitee
    {
        get => _invitee;
        set => this.RaiseAndSetIfChanged(ref _invitee, value);
    }

    public ObservableCollection<CollaborationParticipantItemViewModel> Participants { get; } = new();

    public ReactiveCommand<Unit, Unit> StartSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> JoinSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> LeaveSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> ShareSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> InviteParticipantCommand { get; }

    public void Dispose()
    {
        _collaboration.SessionChanged -= OnSessionChanged;
        _collaboration.ParticipantsChanged -= OnParticipantsChanged;
        _disposables.Dispose();
    }

    private static string CreateShareLinkDisplay(string link)
    {
        return "Share link: " + link;
    }

    private async Task StartSessionAsync()
    {
        await _collaboration.StartSessionAsync(CancellationToken.None);
    }

    private async Task JoinSessionAsync()
    {
        string? sessionId = JoinSessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        bool joined = await _collaboration.JoinSessionAsync(sessionId.Trim(), CancellationToken.None);
        if (!joined)
        {
            await _window.ShowWarningMessageAsync(
                "Unable to join the collaboration session.",
                CancellationToken.None);
        }
    }

    private async Task LeaveSessionAsync()
    {
        await _collaboration.LeaveSessionAsync(CancellationToken.None);
    }

    private async Task ShareSessionAsync()
    {
        string? link = await _collaboration.CreateShareLinkAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(link))
        {
            await _window.ShowWarningMessageAsync(
                "Start or join a session before sharing a link.",
                CancellationToken.None);
            return;
        }

        await _window.ShowInformationMessageAsync(
            CreateShareLinkDisplay(link),
            CancellationToken.None);
    }

    private async Task InviteParticipantAsync()
    {
        string? invitee = Invitee;
        if (string.IsNullOrWhiteSpace(invitee))
        {
            return;
        }

        string trimmedInvitee = invitee.Trim();
        bool invited = await _collaboration.InviteAsync(trimmedInvitee, CancellationToken.None);
        if (!invited)
        {
            await _window.ShowWarningMessageAsync(
                "Unable to send invite. Start or join a collaboration session first.",
                CancellationToken.None);
            return;
        }

        await _window.ShowInformationMessageAsync(
            "Invite prepared for " + trimmedInvitee,
            CancellationToken.None);
        Invitee = string.Empty;
    }

    private void OnSessionChanged(object? sender, CollaborationSessionChangedEventArgs e)
    {
        _ = RxApp.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            ApplySessionState(e.IsSessionActive, e.SessionId, e.StatusMessage);
            return Disposable.Empty;
        });
    }

    private void OnParticipantsChanged(object? sender, CollaborationParticipantsChangedEventArgs e)
    {
        IReadOnlyList<CollaborationParticipantInfo> snapshot = e.Participants;
        _ = RxApp.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            ApplyParticipants(snapshot);
            return Disposable.Empty;
        });
    }

    private void ApplySessionState(bool isSessionActive, string? sessionId, string statusMessage)
    {
        IsSessionActive = isSessionActive;
        SessionId = sessionId;
        StatusMessage = string.IsNullOrWhiteSpace(statusMessage)
            ? "Not connected"
            : statusMessage;

        if (!isSessionActive)
        {
            JoinSessionId = string.Empty;
            Invitee = string.Empty;
        }
        else if (string.IsNullOrWhiteSpace(JoinSessionId))
        {
            JoinSessionId = sessionId;
        }
    }

    private void ApplyParticipants(IReadOnlyList<CollaborationParticipantInfo> participants)
    {
        Participants.Clear();
        foreach (CollaborationParticipantInfo participant in participants)
        {
            Participants.Add(new CollaborationParticipantItemViewModel(
                participant.Id,
                participant.DisplayName,
                participant.Color,
                participant.IsLocal,
                participant.CurrentFile,
                participant.CaretLine,
                participant.CaretColumn));
        }
    }
}
