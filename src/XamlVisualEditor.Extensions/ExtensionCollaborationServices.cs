namespace XamlVisualEditor.Extensions;

/// <summary>Represents collaboration participant state.</summary>
public sealed record CollaborationParticipantInfo(
    string Id,
    string DisplayName,
    string Color,
    bool IsLocal,
    string? CurrentFile,
    int CaretLine,
    int CaretColumn);

/// <summary>Provides collaboration session change details.</summary>
public sealed class CollaborationSessionChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public CollaborationSessionChangedEventArgs(
        bool isSessionActive,
        string? sessionId,
        string statusMessage)
    {
        IsSessionActive = isSessionActive;
        SessionId = sessionId;
        StatusMessage = statusMessage;
    }

    /// <summary>Gets whether a collaboration session is active.</summary>
    public bool IsSessionActive { get; }

    /// <summary>Gets the active session identifier.</summary>
    public string? SessionId { get; }

    /// <summary>Gets the host status message.</summary>
    public string StatusMessage { get; }
}

/// <summary>Provides collaboration participant change details.</summary>
public sealed class CollaborationParticipantsChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public CollaborationParticipantsChangedEventArgs(IReadOnlyList<CollaborationParticipantInfo> participants)
    {
        Participants = participants ?? Array.Empty<CollaborationParticipantInfo>();
    }

    /// <summary>Gets participant snapshot.</summary>
    public IReadOnlyList<CollaborationParticipantInfo> Participants { get; }
}

/// <summary>Provides collaboration session services to extensions.</summary>
public interface ICollaborationHost
{
    /// <summary>Gets whether a session is active.</summary>
    bool IsSessionActive { get; }

    /// <summary>Gets the active session identifier.</summary>
    string? SessionId { get; }

    /// <summary>Gets the collaboration status message.</summary>
    string StatusMessage { get; }

    /// <summary>Gets the current participant snapshot.</summary>
    IReadOnlyList<CollaborationParticipantInfo> GetParticipants();

    /// <summary>Raised when session status changes.</summary>
    event EventHandler<CollaborationSessionChangedEventArgs>? SessionChanged;

    /// <summary>Raised when participant state changes.</summary>
    event EventHandler<CollaborationParticipantsChangedEventArgs>? ParticipantsChanged;

    /// <summary>Starts a new collaboration session.</summary>
    Task StartSessionAsync(CancellationToken cancellationToken);

    /// <summary>Joins an existing collaboration session.</summary>
    Task<bool> JoinSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Leaves the current collaboration session.</summary>
    Task LeaveSessionAsync(CancellationToken cancellationToken);

    /// <summary>Creates a share link for the active session.</summary>
    Task<string?> CreateShareLinkAsync(CancellationToken cancellationToken);

    /// <summary>Sends an invite for the active session.</summary>
    Task<bool> InviteAsync(string invitee, CancellationToken cancellationToken);
}

/// <summary>Represents the collaboration panel model exposed to extensions.</summary>
public interface ICollaborationPanelModel
{
}

/// <summary>Provides access to the collaboration panel model.</summary>
public interface ICollaborationPanelHost
{
    /// <summary>Gets the collaboration panel model.</summary>
    ICollaborationPanelModel? PanelModel { get; }
}
