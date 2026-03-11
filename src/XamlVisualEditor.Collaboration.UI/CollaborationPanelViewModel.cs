using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Collaboration;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Collaboration.UI;

/// <summary>
/// Represents a participant in a collaboration session.
/// </summary>
public sealed class ParticipantViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the participant identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Reactive]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assigned color (for cursor/selection highlighting).
    /// </summary>
    [Reactive]
    public string Color { get; set; } = "#0078D4";

    /// <summary>
    /// Gets or sets whether this participant is the local user.
    /// </summary>
    public bool IsLocal { get; init; }

    /// <summary>
    /// Gets or sets the participant's current file path.
    /// </summary>
    [Reactive]
    public string? CurrentFile { get; set; }

    /// <summary>
    /// Gets or sets the participant's caret line.
    /// </summary>
    [Reactive]
    public int CaretLine { get; set; }

    /// <summary>
    /// Gets or sets the participant's caret column.
    /// </summary>
    [Reactive]
    public int CaretColumn { get; set; }

    public ParticipantViewModel(string id)
    {
        Id = id;
    }
}

/// <summary>
/// ViewModel for the collaboration panel UI.
/// </summary>
public sealed class CollaborationPanelViewModel : ReactiveObject, IDisposable, ICollaborationPanelModel
{
    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    /// Gets or sets whether a session is active.
    /// </summary>
    [Reactive]
    public bool IsSessionActive { get; set; }

    /// <summary>
    /// Gets or sets the session URL or identifier.
    /// </summary>
    [Reactive]
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the local user's display name.
    /// </summary>
    [Reactive]
    public string LocalUserName { get; set; } = Environment.UserName;

    /// <summary>
    /// Gets the list of participants.
    /// </summary>
    public ObservableCollection<ParticipantViewModel> Participants { get; } = new();

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    [Reactive]
    public string StatusMessage { get; set; } = "Not connected";

    /// <summary>
    /// Command to start a new collaboration session (host).
    /// </summary>
    public ReactiveCommand<Unit, Unit> StartSessionCommand { get; }

    /// <summary>
    /// Command to join an existing session.
    /// </summary>
    public ReactiveCommand<Unit, Unit> JoinSessionCommand { get; }

    /// <summary>
    /// Command to leave the current session.
    /// </summary>
    public ReactiveCommand<Unit, Unit> LeaveSessionCommand { get; }

    /// <summary>
    /// Command to copy the session URL to clipboard.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CopySessionLinkCommand { get; }

    private readonly XamlCollabBridge? _bridge;

    public CollaborationPanelViewModel(XamlCollabBridge? bridge = null)
    {
        _bridge = bridge;

        IObservable<bool> canLeave = this.WhenAnyValue(x => x.IsSessionActive);
        IObservable<bool> canStart = this.WhenAnyValue(x => x.IsSessionActive, active => !active);

        StartSessionCommand = ReactiveCommand.Create(StartSession, canStart);
        JoinSessionCommand = ReactiveCommand.Create(JoinSession, canStart);
        LeaveSessionCommand = ReactiveCommand.Create(LeaveSession, canLeave);
        CopySessionLinkCommand = ReactiveCommand.Create(CopySessionLink, canLeave);
    }

    private void StartSession()
    {
        SessionId = Guid.NewGuid().ToString("N")[..8];
        IsSessionActive = true;
        StatusMessage = $"Hosting session {SessionId}";

        // Add self as participant
        ParticipantViewModel localUser = new(_bridge?.LocalParticipantId ?? Guid.NewGuid().ToString("N"))
        {
            DisplayName = LocalUserName,
            IsLocal = true,
            Color = "#0078D4"
        };
        Participants.Add(localUser);
    }

    private void JoinSession()
    {
        string? sessionId = SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        IsSessionActive = true;
        StatusMessage = $"Joined session {sessionId}";

        // Add self as participant
        ParticipantViewModel localUser = new(_bridge?.LocalParticipantId ?? Guid.NewGuid().ToString("N"))
        {
            DisplayName = LocalUserName,
            IsLocal = true,
            Color = "#0078D4"
        };
        Participants.Add(localUser);
    }

    private void LeaveSession()
    {
        IsSessionActive = false;
        SessionId = null;
        StatusMessage = "Not connected";
        Participants.Clear();
    }

    private void CopySessionLink()
    {
        // In a real implementation, this would use IClipboard
        // For now, the session ID is available via the SessionId property
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
