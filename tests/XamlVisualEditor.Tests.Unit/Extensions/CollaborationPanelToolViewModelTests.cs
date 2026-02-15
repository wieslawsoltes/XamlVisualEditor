using System.Reactive.Threading.Tasks;
using XamlVisualEditor.CollaborationExtension;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

#pragma warning disable CS0067

public sealed class CollaborationPanelToolViewModelTests
{
    [Fact]
    public void Constructor_UsesCurrentHostSnapshot()
    {
        StubCollaborationHost host = new(isSessionActive: true, sessionId: "sess-1", statusMessage: "Connected");
        host.SetParticipants(new[]
        {
            new CollaborationParticipantInfo("local", "Local User", "#0078D4", true, "Main.axaml", 10, 2)
        });

        InMemoryWindow window = new();
        CollaborationPanelToolViewModel viewModel = new(host, window);

        Assert.True(viewModel.IsSessionActive);
        Assert.Equal("sess-1", viewModel.SessionId);
        Assert.Equal("Connected", viewModel.StatusMessage);
        Assert.Single(viewModel.Participants);
        Assert.Equal("Local User", viewModel.Participants[0].DisplayName);
    }

    [Fact]
    public async Task JoinSession_ShowsWarning_WhenJoinFails()
    {
        StubCollaborationHost host = new(isSessionActive: false, sessionId: null, statusMessage: "Offline")
        {
            JoinResult = false
        };

        InMemoryWindow window = new();
        CollaborationPanelToolViewModel viewModel = new(host, window)
        {
            JoinSessionId = "session-a"
        };

        await viewModel.JoinSessionCommand.Execute().ToTask();

        Assert.Equal("session-a", host.LastJoinSessionId);
        Assert.Contains(window.Messages, message => message.Contains("Unable to join", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InviteParticipant_ClearsInvitee_WhenInviteSucceeds()
    {
        StubCollaborationHost host = new(isSessionActive: true, sessionId: "sess-2", statusMessage: "Connected")
        {
            InviteResult = true
        };

        InMemoryWindow window = new();
        CollaborationPanelToolViewModel viewModel = new(host, window)
        {
            Invitee = "teammate@example.com"
        };

        await viewModel.InviteParticipantCommand.Execute().ToTask();

        Assert.Equal("teammate@example.com", host.LastInvitee);
        Assert.Equal(string.Empty, viewModel.Invitee);
        Assert.Contains(window.Messages, message => message.Contains("Invite prepared", StringComparison.Ordinal));
    }

    [Fact]
    public void SessionChangedEvent_RefreshesViewModelState()
    {
        StubCollaborationHost host = new(isSessionActive: false, sessionId: null, statusMessage: "Offline");
        InMemoryWindow window = new();
        CollaborationPanelToolViewModel viewModel = new(host, window);

        host.RaiseSessionChanged(isSessionActive: true, sessionId: "sess-3", statusMessage: "Hosting session sess-3");

        Assert.True(viewModel.IsSessionActive);
        Assert.Equal("sess-3", viewModel.SessionId);
        Assert.Equal("Hosting session sess-3", viewModel.StatusMessage);
    }

    private sealed class StubCollaborationHost : ICollaborationHost
    {
        private readonly List<CollaborationParticipantInfo> _participants = new();

        public StubCollaborationHost(bool isSessionActive, string? sessionId, string statusMessage)
        {
            IsSessionActive = isSessionActive;
            SessionId = sessionId;
            StatusMessage = statusMessage;
        }

        public bool JoinResult { get; set; } = true;

        public bool InviteResult { get; set; } = true;

        public bool IsSessionActive { get; private set; }

        public string? SessionId { get; private set; }

        public string StatusMessage { get; private set; }

        public string? LastJoinSessionId { get; private set; }

        public string? LastInvitee { get; private set; }

        public event EventHandler<CollaborationSessionChangedEventArgs>? SessionChanged;

        public event EventHandler<CollaborationParticipantsChangedEventArgs>? ParticipantsChanged;

        public IReadOnlyList<CollaborationParticipantInfo> GetParticipants()
        {
            return _participants;
        }

        public Task StartSessionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RaiseSessionChanged(true, "start-1", "Hosting session start-1");
            return Task.CompletedTask;
        }

        public Task<bool> JoinSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastJoinSessionId = sessionId;
            if (JoinResult)
            {
                RaiseSessionChanged(true, sessionId, "Joined session " + sessionId);
            }

            return Task.FromResult(JoinResult);
        }

        public Task LeaveSessionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RaiseSessionChanged(false, null, "Not connected");
            return Task.CompletedTask;
        }

        public Task<string?> CreateShareLinkAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSessionActive || string.IsNullOrWhiteSpace(SessionId))
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>("xve://collaboration/" + SessionId);
        }

        public Task<bool> InviteAsync(string invitee, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastInvitee = invitee;
            return Task.FromResult(InviteResult);
        }

        public void SetParticipants(IEnumerable<CollaborationParticipantInfo> participants)
        {
            _participants.Clear();
            _participants.AddRange(participants);
            ParticipantsChanged?.Invoke(this, new CollaborationParticipantsChangedEventArgs(_participants));
        }

        public void RaiseSessionChanged(bool isSessionActive, string? sessionId, string statusMessage)
        {
            IsSessionActive = isSessionActive;
            SessionId = sessionId;
            StatusMessage = statusMessage;
            SessionChanged?.Invoke(this, new CollaborationSessionChangedEventArgs(isSessionActive, sessionId, statusMessage));
        }
    }
}

#pragma warning restore CS0067
