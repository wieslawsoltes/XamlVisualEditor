using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Debugging.Dap;

public sealed class DapProtocolClient : IAsyncDisposable
{
    private readonly DapMessageReader _reader;
    private readonly DapMessageWriter _writer;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _sequence;
    private CancellationTokenSource? _loopCts;
    private Task? _readLoop;

    public DapProtocolClient(DapMessageReader reader, DapMessageWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public event Action<string, JsonElement>? EventReceived;

    public void Start(CancellationToken ct)
    {
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readLoop = Task.Run(() => ReadLoopAsync(_loopCts.Token), _loopCts.Token);
    }

    public async Task<JsonElement> SendRequestAsync(string command, object? arguments, CancellationToken ct)
    {
        int seq = Interlocked.Increment(ref _sequence);
        TaskCompletionSource<JsonElement> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[seq] = tcs;

        var payload = new
        {
            seq,
            type = "request",
            command,
            arguments
        };

        string json = JsonSerializer.Serialize(payload);
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteMessageAsync(json, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }

        return await tcs.Task.ConfigureAwait(false);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? json = await _reader.ReadMessageAsync(ct).ConfigureAwait(false);
            if (json is null)
            {
                break;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("type", out JsonElement typeElement))
            {
                continue;
            }

            string? type = typeElement.GetString();
            if (string.Equals(type, "response", StringComparison.OrdinalIgnoreCase))
            {
                HandleResponse(root);
                continue;
            }

            if (string.Equals(type, "event", StringComparison.OrdinalIgnoreCase))
            {
                HandleEvent(root);
            }
        }
    }

    private void HandleResponse(JsonElement root)
    {
        if (!root.TryGetProperty("request_seq", out JsonElement requestSeqElement))
        {
            return;
        }

        int requestSeq = requestSeqElement.GetInt32();
        if (!_pending.TryRemove(requestSeq, out TaskCompletionSource<JsonElement>? tcs))
        {
            return;
        }

        bool success = !root.TryGetProperty("success", out JsonElement successElement)
            || successElement.GetBoolean();

        if (!success)
        {
            string? message = root.TryGetProperty("message", out JsonElement messageElement)
                ? messageElement.GetString()
                : "DAP request failed";
            tcs.TrySetException(new InvalidOperationException(message));
            return;
        }

        JsonElement body = root.TryGetProperty("body", out JsonElement bodyElement)
            ? bodyElement.Clone()
            : default;
        tcs.TrySetResult(body);
    }

    private void HandleEvent(JsonElement root)
    {
        if (!root.TryGetProperty("event", out JsonElement eventElement))
        {
            return;
        }

        string? name = eventElement.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        JsonElement body = root.TryGetProperty("body", out JsonElement bodyElement)
            ? bodyElement.Clone()
            : default;
        EventReceived?.Invoke(name, body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_loopCts is not null)
        {
            _loopCts.Cancel();
        }

        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        foreach (TaskCompletionSource<JsonElement> pending in _pending.Values)
        {
            pending.TrySetCanceled();
        }

        _sendLock.Dispose();
        _loopCts?.Dispose();
    }
}
