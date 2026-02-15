using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed collaboration services adapter.</summary>
public sealed class CollaborationPanelHostAdapter : ICollaborationPanelHost, ICollaborationHost, IDisposable
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly CompositeDisposable _disposables = new();
    private readonly Dictionary<ParticipantViewModel, IDisposable> _participantSubscriptions = new();

    public CollaborationPanelHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        CollaborationPanelViewModel panel = _mainViewModel.Collaboration;

        IDisposable sessionSubscription = panel.WhenAnyValue(
                vm => vm.IsSessionActive,
                vm => vm.SessionId,
                vm => vm.StatusMessage)
            .Subscribe(_ => PublishSessionChanged());
        _disposables.Add(sessionSubscription);

        panel.Participants.CollectionChanged += OnParticipantsChanged;
        _disposables.Add(Disposable.Create(() => panel.Participants.CollectionChanged -= OnParticipantsChanged));

        TrackParticipants(panel.Participants);
        PublishSessionChanged();
        PublishParticipantsChanged();
    }

    public object? ViewModel => _mainViewModel.Collaboration;

    public bool IsSessionActive => _mainViewModel.Collaboration.IsSessionActive;

    public string? SessionId => _mainViewModel.Collaboration.SessionId;

    public string StatusMessage => _mainViewModel.Collaboration.StatusMessage;

    public event EventHandler<CollaborationSessionChangedEventArgs>? SessionChanged;

    public event EventHandler<CollaborationParticipantsChangedEventArgs>? ParticipantsChanged;

    public IReadOnlyList<CollaborationParticipantInfo> GetParticipants()
    {
        return GetParticipantsCore();
    }

    public async Task StartSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _mainViewModel.Collaboration.StartSessionCommand
            .Execute()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> JoinSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        CollaborationPanelViewModel panel = _mainViewModel.Collaboration;
        panel.SessionId = sessionId.Trim();
        await panel.JoinSessionCommand
            .Execute()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);
        return panel.IsSessionActive;
    }

    public async Task LeaveSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _mainViewModel.Collaboration.LeaveSessionCommand
            .Execute()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> CreateShareLinkAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CollaborationPanelViewModel panel = _mainViewModel.Collaboration;
        if (!panel.IsSessionActive || string.IsNullOrWhiteSpace(panel.SessionId))
        {
            return null;
        }

        await panel.CopySessionLinkCommand
            .Execute()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);
        return "xve://collaboration/" + panel.SessionId.Trim();
    }

    public Task<bool> InviteAsync(string invitee, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSessionActive || string.IsNullOrWhiteSpace(invitee))
        {
            return Task.FromResult(false);
        }

        string inviteeText = invitee.Trim();
        _mainViewModel.Collaboration.StatusMessage = "Invite prepared for " + inviteeText;
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _participantSubscriptions.Values)
        {
            subscription.Dispose();
        }

        _participantSubscriptions.Clear();
        _disposables.Dispose();
    }

    private void OnParticipantsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        TrackParticipants(_mainViewModel.Collaboration.Participants);
        PublishParticipantsChanged();
    }

    private void TrackParticipants(IEnumerable<ParticipantViewModel> participants)
    {
        HashSet<ParticipantViewModel> active = new(participants);
        foreach (ParticipantViewModel existing in _participantSubscriptions.Keys.ToList())
        {
            if (active.Contains(existing))
            {
                continue;
            }

            _participantSubscriptions[existing].Dispose();
            _participantSubscriptions.Remove(existing);
        }

        foreach (ParticipantViewModel participant in active)
        {
            if (_participantSubscriptions.ContainsKey(participant))
            {
                continue;
            }

            IDisposable subscription = participant.WhenAnyValue(
                    vm => vm.DisplayName,
                    vm => vm.Color,
                    vm => vm.CurrentFile,
                    vm => vm.CaretLine,
                    vm => vm.CaretColumn)
                .Subscribe(_ => PublishParticipantsChanged());
            _participantSubscriptions[participant] = subscription;
        }
    }

    private void PublishSessionChanged()
    {
        SessionChanged?.Invoke(this, new CollaborationSessionChangedEventArgs(
            IsSessionActive,
            SessionId,
            StatusMessage));
    }

    private void PublishParticipantsChanged()
    {
        ParticipantsChanged?.Invoke(this, new CollaborationParticipantsChangedEventArgs(GetParticipantsCore()));
    }

    private IReadOnlyList<CollaborationParticipantInfo> GetParticipantsCore()
    {
        List<CollaborationParticipantInfo> participants = new(_mainViewModel.Collaboration.Participants.Count);
        foreach (ParticipantViewModel participant in _mainViewModel.Collaboration.Participants)
        {
            participants.Add(new CollaborationParticipantInfo(
                participant.Id,
                participant.DisplayName,
                participant.Color,
                participant.IsLocal,
                participant.CurrentFile,
                participant.CaretLine,
                participant.CaretColumn));
        }

        return participants;
    }
}
