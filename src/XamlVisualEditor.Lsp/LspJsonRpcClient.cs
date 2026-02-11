using System.Collections.Concurrent;
using System.Text.Json;

namespace XamlVisualEditor.Lsp;

internal sealed class LspJsonRpcClient : IAsyncDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _readerTask;
    private int _nextId;

    public LspJsonRpcClient(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public event Action<string, JsonElement>? NotificationReceived;
    public event Action<Exception>? Disconnected;

    public void Start()
    {
        if (_readerTask is not null)
        {
            return;
        }

        _readerTask = Task.Run(() => ReaderLoopAsync(_cts.Token));
    }

    public async Task<JsonElement?> SendRequestAsync(string method, object? @params, CancellationToken ct)
    {
        int id = Interlocked.Increment(ref _nextId);
        TaskCompletionSource<JsonDocument> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, tcs))
        {
            throw new InvalidOperationException("Failed to register LSP request.");
        }

        await LspMessageFraming.WriteMessageAsync(_output, new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        }, ct).ConfigureAwait(false);

        JsonDocument response = await tcs.Task.WaitAsync(ct).ConfigureAwait(false);
        using (response)
        {
            if (response.RootElement.TryGetProperty("error", out JsonElement error))
            {
                throw new LspJsonRpcException(error.ToString());
            }

            if (response.RootElement.TryGetProperty("result", out JsonElement result))
            {
                return result.Clone();
            }

            return null;
        }
    }

    public Task SendNotificationAsync(string method, object? @params, CancellationToken ct)
    {
        return LspMessageFraming.WriteMessageAsync(_output, new
        {
            jsonrpc = "2.0",
            method,
            @params
        }, ct);
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
                JsonDocument message = await LspMessageFraming.ReadMessageAsync(_input, ct).ConfigureAwait(false);

                if (message.RootElement.TryGetProperty("method", out JsonElement methodElement))
                {
                    string? method = methodElement.GetString();
                    if (method is not null && !message.RootElement.TryGetProperty("id", out JsonElement _))
                    {
                        JsonElement paramsElement = default;
                        if (message.RootElement.TryGetProperty("params", out JsonElement parsed))
                        {
                            paramsElement = parsed;
                        }

                        NotificationReceived?.Invoke(method, paramsElement);
                        message.Dispose();
                        continue;
                    }
                }

                if (!message.RootElement.TryGetProperty("id", out JsonElement idElement))
                {
                    message.Dispose();
                    continue;
                }

                int? id = GetId(idElement);
                if (id is null)
                {
                    message.Dispose();
                    continue;
                }

                if (_pending.TryRemove(id.Value, out TaskCompletionSource<JsonDocument>? tcs))
                {
                    tcs.TrySetResult(message);
                    continue;
                }

                message.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            FailPendingRequests(ex);
            Disconnected?.Invoke(ex);
        }
    }

    private void FailPendingRequests(Exception ex)
    {
        foreach (TaskCompletionSource<JsonDocument> pending in _pending.Values)
        {
            pending.TrySetException(ex);
        }

        _pending.Clear();
    }

    private static int? GetId(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt32(),
            JsonValueKind.String => int.TryParse(element.GetString(), out int id) ? id : null,
            _ => null
        };
    }
}

internal sealed class LspJsonRpcException : Exception
{
    public LspJsonRpcException(string message)
        : base(message)
    {
    }
}
