using System.Collections.Concurrent;
using System.Text.Json;

namespace IdeBridgeCli;

internal sealed class JsonRpcClient : IAsyncDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonDocument>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _readerTask;
    private long _nextId;

    public JsonRpcClient(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public event Action<string, JsonElement?>? NotificationReceived;

    public void Start()
    {
        if (_readerTask is not null)
        {
            return;
        }

        _readerTask = Task.Run(() => ReaderLoopAsync(_cts.Token));
    }

    public async Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct)
    {
        long id = Interlocked.Increment(ref _nextId);
        TaskCompletionSource<JsonDocument> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await IdeBridgeMessageFraming.WriteMessageAsync(_output, new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params = parameters
        }, ct).ConfigureAwait(false);

        JsonDocument response = await tcs.Task.WaitAsync(ct).ConfigureAwait(false);
        using (response)
        {
            if (response.RootElement.TryGetProperty("error", out JsonElement error))
            {
                throw new InvalidOperationException(error.ToString());
            }

            if (response.RootElement.TryGetProperty("result", out JsonElement result))
            {
                return result.Clone();
            }

            return default;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_readerTask is not null)
        {
            try
            {
                await _readerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (TaskCompletionSource<JsonDocument> pending in _pending.Values)
        {
            pending.TrySetCanceled();
        }

        _pending.Clear();
        _cts.Dispose();
    }

    private async Task ReaderLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using JsonDocument message = await IdeBridgeMessageFraming.ReadMessageAsync(_input, ct).ConfigureAwait(false);
                JsonElement root = message.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (root.TryGetProperty("method", out JsonElement methodElement))
                {
                    string? method = methodElement.GetString();
                    if (method is not null && !root.TryGetProperty("id", out JsonElement _))
                    {
                        JsonElement? payload = root.TryGetProperty("params", out JsonElement paramsElement)
                            ? paramsElement.Clone()
                            : null;
                        NotificationReceived?.Invoke(method, payload);
                        continue;
                    }
                }

                if (!root.TryGetProperty("id", out JsonElement idElement))
                {
                    continue;
                }

                long? id = GetId(idElement);
                if (id is null)
                {
                    continue;
                }

                if (_pending.TryRemove(id.Value, out TaskCompletionSource<JsonDocument>? tcs))
                {
                    JsonDocument clone = JsonDocument.Parse(root.GetRawText());
                    tcs.TrySetResult(clone);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static long? GetId(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out long id) ? id : null,
            JsonValueKind.String => long.TryParse(element.GetString(), out long id) ? id : null,
            _ => null
        };
    }
}
