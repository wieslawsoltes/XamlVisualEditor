using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Collaboration;

// ==============================================
// 8.1.4 — CollabUndoRedoService
// ==============================================

/// <summary>
/// Provides collaborative undo/redo that tracks changes per-participant
/// and coordinates undo operations across multiple users.
/// </summary>
public sealed class CollabUndoRedoService : IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentStack<CollabUndoBatch>> _participantUndoStacks = new();
    private readonly ConcurrentDictionary<string, ConcurrentStack<CollabUndoBatch>> _participantRedoStacks = new();
    private readonly XamlCollabBridge _bridge;
    private CollabUndoBatch? _pendingBatch;

    /// <summary>
    /// Gets whether the local participant can undo.
    /// </summary>
    public bool CanUndo => !GetUndoStack(_bridge.LocalParticipantId).IsEmpty;

    /// <summary>
    /// Gets whether the local participant can redo.
    /// </summary>
    public bool CanRedo => !GetRedoStack(_bridge.LocalParticipantId).IsEmpty;

    public CollabUndoRedoService(XamlCollabBridge bridge)
    {
        _bridge = bridge;
    }

    /// <summary>
    /// Begins recording changes for a new undo batch.
    /// </summary>
    public void BeginBatch(string description)
    {
        _pendingBatch = new CollabUndoBatch(
            _bridge.LocalParticipantId,
            description,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Records a change in the current batch.
    /// </summary>
    public void RecordChange(AstChange change)
    {
        _pendingBatch?.Changes.Add(change);
    }

    /// <summary>
    /// Commits the current batch to the undo stack.
    /// </summary>
    public void CommitBatch()
    {
        if (_pendingBatch is null || _pendingBatch.Changes.Count == 0)
        {
            _pendingBatch = null;
            return;
        }

        ConcurrentStack<CollabUndoBatch> undoStack = GetUndoStack(_pendingBatch.ParticipantId);
        undoStack.Push(_pendingBatch);

        // Clear redo stack for this participant since new changes were made
        GetRedoStack(_pendingBatch.ParticipantId).Clear();

        _pendingBatch = null;
    }

    /// <summary>
    /// Undoes the last batch for the local participant.
    /// </summary>
    /// <returns>The inverse changes to apply, or null if nothing to undo.</returns>
    public IReadOnlyList<AstChange>? Undo()
    {
        return UndoForParticipant(_bridge.LocalParticipantId);
    }

    /// <summary>
    /// Redoes the last undone batch for the local participant.
    /// </summary>
    /// <returns>The changes to re-apply, or null if nothing to redo.</returns>
    public IReadOnlyList<AstChange>? Redo()
    {
        return RedoForParticipant(_bridge.LocalParticipantId);
    }

    /// <summary>
    /// Undoes the last batch for a specific participant.
    /// </summary>
    public IReadOnlyList<AstChange>? UndoForParticipant(string participantId)
    {
        ConcurrentStack<CollabUndoBatch> undoStack = GetUndoStack(participantId);
        if (!undoStack.TryPop(out CollabUndoBatch? batch))
        {
            return null;
        }

        GetRedoStack(participantId).Push(batch);

        // Return inverse changes (reversed order)
        return batch.Changes
            .Select(InvertChange)
            .Reverse()
            .ToList();
    }

    /// <summary>
    /// Redoes the last undone batch for a specific participant.
    /// </summary>
    public IReadOnlyList<AstChange>? RedoForParticipant(string participantId)
    {
        ConcurrentStack<CollabUndoBatch> redoStack = GetRedoStack(participantId);
        if (!redoStack.TryPop(out CollabUndoBatch? batch))
        {
            return null;
        }

        GetUndoStack(participantId).Push(batch);

        return batch.Changes.ToList();
    }

    /// <summary>
    /// Clears all undo/redo history for all participants.
    /// </summary>
    public void Clear()
    {
        _participantUndoStacks.Clear();
        _participantRedoStacks.Clear();
        _pendingBatch = null;
    }

    private ConcurrentStack<CollabUndoBatch> GetUndoStack(string participantId) =>
        _participantUndoStacks.GetOrAdd(participantId, _ => new ConcurrentStack<CollabUndoBatch>());

    private ConcurrentStack<CollabUndoBatch> GetRedoStack(string participantId) =>
        _participantRedoStacks.GetOrAdd(participantId, _ => new ConcurrentStack<CollabUndoBatch>());

    private static AstChange InvertChange(AstChange change) => change switch
    {
        NodeAdded added => new NodeRemoved(added.NodeId, added.ParentId, added.Index),
        NodeRemoved removed => new NodeAdded(removed.NodeId, removed.ParentId, removed.Index, "RemovedNode"),
        NodeMoved moved => new NodeMoved(moved.NodeId, moved.NewParentId, moved.NewIndex, moved.OldParentId, moved.OldIndex),
        PropertyValueChanged pvc => new PropertyValueChanged(pvc.NodeId, pvc.PropertyName, pvc.NewValue, pvc.OldValue),
        TextContentChanged tcc => new TextContentChanged(tcc.NodeId, tcc.NewText, tcc.OldText),
        _ => change
    };

    public void Dispose()
    {
        Clear();
    }
}

/// <summary>
/// A batch of changes that can be undone/redone as a unit.
/// </summary>
public sealed class CollabUndoBatch
{
    /// <summary>Gets the participant who made the changes.</summary>
    public string ParticipantId { get; }

    /// <summary>Gets the description of the batch.</summary>
    public string Description { get; }

    /// <summary>Gets the timestamp when the batch was created.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the changes in this batch.</summary>
    public List<AstChange> Changes { get; } = new();

    public CollabUndoBatch(string participantId, string description, DateTimeOffset timestamp)
    {
        ParticipantId = participantId;
        Description = description;
        Timestamp = timestamp;
    }
}

// ==============================================
// 8.1.5 — SharedFileCollabSession
// ==============================================

/// <summary>
/// A file-based collaboration session for local multi-instance scenarios.
/// Uses a shared directory with JSON change files for synchronization.
/// </summary>
public sealed class SharedFileCollabSession : ICollaborationBridge, IDisposable
{
    private readonly string _sessionDir;
    private readonly string _participantId;
    private readonly FileSystemWatcher _watcher;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<SharedFileCollabSession> _logger;
    private int _sequenceNumber;

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public event Action<IReadOnlyList<AstChange>>? RemoteChangesReceived;

    public SharedFileCollabSession(
        string sessionDirectory,
        string participantId,
        ILogger<SharedFileCollabSession>? logger = null)
    {
        _sessionDir = sessionDirectory;
        _participantId = participantId;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SharedFileCollabSession>.Instance;

        Directory.CreateDirectory(_sessionDir);

        _watcher = new FileSystemWatcher(_sessionDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        _watcher.Created += OnFileCreated;
    }

    /// <summary>
    /// Starts watching for remote changes.
    /// </summary>
    public void Start()
    {
        IsConnected = true;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Stops the session.
    /// </summary>
    public void Stop()
    {
        IsConnected = false;
        _watcher.EnableRaisingEvents = false;
    }

    /// <inheritdoc />
    public async Task SendChangesAsync(IReadOnlyList<AstChange> changes, CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            return;
        }

        int seq = Interlocked.Increment(ref _sequenceNumber);
        string fileName = $"{_participantId}_{seq:D8}.json";
        string filePath = Path.Combine(_sessionDir, fileName);

        CollabChangeEnvelope envelope = new()
        {
            ParticipantId = _participantId,
            Sequence = seq,
            Timestamp = DateTimeOffset.UtcNow,
            ChangeDescriptions = changes.Select(DescribeChange).ToList()
        };

        string json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        // Skip files we wrote ourselves
        string fileName = Path.GetFileNameWithoutExtension(e.Name ?? string.Empty);
        if (fileName.StartsWith(_participantId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            // Read and parse the change file
            string json = File.ReadAllText(e.FullPath);
            CollabChangeEnvelope? envelope = JsonSerializer.Deserialize<CollabChangeEnvelope>(json);

            if (envelope?.ChangeDescriptions is { Count: > 0 })
            {
                // For a real implementation, we would deserialize back to AstChange records.
                // For now, signal that remote changes were received.
                RemoteChangesReceived?.Invoke(Array.Empty<AstChange>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Skipping malformed collab change file: {Message}", ex.Message);
        }
    }

    private static CollabChangeDescription DescribeChange(AstChange change) => change switch
    {
        NodeAdded added => new() { Type = "NodeAdded", NodeId = added.NodeId.ToString(), Details = added.NodeTypeName },
        NodeRemoved removed => new() { Type = "NodeRemoved", NodeId = removed.NodeId.ToString() },
        NodeMoved moved => new() { Type = "NodeMoved", NodeId = moved.NodeId.ToString() },
        PropertyValueChanged pvc => new() { Type = "PropertyChanged", NodeId = pvc.NodeId.ToString(), Details = $"{pvc.PropertyName}={pvc.NewValue}" },
        TextContentChanged tcc => new() { Type = "TextChanged", NodeId = tcc.NodeId.ToString(), Details = tcc.NewText },
        _ => new() { Type = "Unknown" }
    };

    public void Dispose()
    {
        _cts.Cancel();
        _watcher.Dispose();
        _cts.Dispose();
    }
}

/// <summary>
/// Serializable envelope for collaboration change files.
/// </summary>
public sealed class CollabChangeEnvelope
{
    public string ParticipantId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public List<CollabChangeDescription> ChangeDescriptions { get; set; } = new();
}

/// <summary>
/// Serializable description of a single change.
/// </summary>
public sealed class CollabChangeDescription
{
    public string Type { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string? Details { get; set; }
}

// ==============================================
// 8.1.6 — CollabRealtimeSession (WebSocket)
// ==============================================

/// <summary>
/// A WebSocket-based real-time collaboration session for remote participants.
/// Connects to a relay server and transmits/receives collaboration operations.
/// </summary>
public sealed class CollabRealtimeSession : ICollaborationBridge, IDisposable, IAsyncDisposable
{
    private readonly string _serverUri;
    private readonly string _sessionId;
    private readonly string _participantId;
    private readonly ILogger<CollabRealtimeSession> _logger;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public event Action<IReadOnlyList<AstChange>>? RemoteChangesReceived;

    /// <summary>
    /// Raised when the connection status changes.
    /// </summary>
    public event Action<bool>? ConnectionStatusChanged;

    public CollabRealtimeSession(
        string serverUri,
        string sessionId,
        string participantId,
        ILogger<CollabRealtimeSession>? logger = null)
    {
        _serverUri = serverUri;
        _sessionId = sessionId;
        _participantId = participantId;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CollabRealtimeSession>.Instance;
    }

    /// <summary>
    /// Connects to the collaboration server.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _socket = new ClientWebSocket();
        _socket.Options.AddSubProtocol("xaml-collab-v1");

        Uri uri = new($"{_serverUri}?session={_sessionId}&participant={_participantId}");
        await _socket.ConnectAsync(uri, ct);

        IsConnected = true;
        ConnectionStatusChanged?.Invoke(true);

        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
    }

    /// <summary>
    /// Disconnects from the collaboration server.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _receiveCts?.Cancel();

        if (_socket?.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Leaving", ct);
        }

        IsConnected = false;
        ConnectionStatusChanged?.Invoke(false);
    }

    /// <inheritdoc />
    public async Task SendChangesAsync(IReadOnlyList<AstChange> changes, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open)
        {
            return;
        }

        CollabChangeEnvelope envelope = new()
        {
            ParticipantId = _participantId,
            Timestamp = DateTimeOffset.UtcNow,
            ChangeDescriptions = changes.Select(DescribeChange).ToList()
        };

        string json = JsonSerializer.Serialize(envelope);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        await _socket.SendAsync(
            new ArraySegment<byte>(buffer),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: ct);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        byte[] buffer = new byte[8192];

        while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
        {
            try
            {
                // Accumulate multi-frame messages
                using MemoryStream messageStream = new();
                WebSocketReceiveResult result;

                do
                {
                    result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        IsConnected = false;
                        ConnectionStatusChanged?.Invoke(false);
                        return;
                    }

                    messageStream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string json = Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);
                    CollabChangeEnvelope? envelope = JsonSerializer.Deserialize<CollabChangeEnvelope>(json);

                    if (envelope is not null && envelope.ParticipantId != _participantId)
                    {
                        // Signal that remote changes arrived
                        RemoteChangesReceived?.Invoke(Array.Empty<AstChange>());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning("WebSocket connection lost: {Message}", ex.Message);
                IsConnected = false;
                ConnectionStatusChanged?.Invoke(false);
                break;
            }
        }
    }

    private static CollabChangeDescription DescribeChange(AstChange change) => change switch
    {
        NodeAdded added => new() { Type = "NodeAdded", NodeId = added.NodeId.ToString(), Details = added.NodeTypeName },
        NodeRemoved removed => new() { Type = "NodeRemoved", NodeId = removed.NodeId.ToString() },
        PropertyValueChanged pvc => new() { Type = "PropertyChanged", NodeId = pvc.NodeId.ToString(), Details = $"{pvc.PropertyName}={pvc.NewValue}" },
        _ => new() { Type = "Unknown" }
    };

    public async ValueTask DisposeAsync()
    {
        _receiveCts?.Cancel();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }

        _receiveCts?.Dispose();
        _socket?.Dispose();
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();

        if (_receiveTask is not null)
        {
            try
            {
                _receiveTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }

        _receiveCts?.Dispose();
        _socket?.Dispose();
    }
}
