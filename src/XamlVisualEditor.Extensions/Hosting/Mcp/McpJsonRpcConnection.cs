using System.Collections.Concurrent;
using System.Text.Json;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Handles a JSON-RPC connection over a stream transport.</summary>
public sealed class McpJsonRpcConnection : IAsyncDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly bool _ownsStreams;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly McpRequestRouter _router;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    public McpJsonRpcConnection(Stream input, Stream output, McpRequestRouter router, bool ownsStreams = false)
    {
        _input = input;
        _output = output;
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _ownsStreams = ownsStreams;
    }

    public event Action<string, JsonElement?>? NotificationReceived;

    public event Action<Exception?>? Disconnected;

    public void Start(CancellationToken ct)
    {
        if (_readLoop is not null)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    public Task SendNotificationAsync(string method, object? parameters, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method name is required.", nameof(method));
        }

        var payload = new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters
        };

        return SendAsync(payload, ct);
    }

    private async Task SendAsync(object payload, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await McpMessageFraming.WriteMessageAsync(_output, payload, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        Exception? error = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using JsonDocument message = await McpMessageFraming.ReadMessageAsync(_input, ct).ConfigureAwait(false);
                JsonElement root = message.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                bool hasId = root.TryGetProperty("id", out JsonElement idElement);
                bool hasMethod = root.TryGetProperty("method", out JsonElement methodElement);

                if (hasId && !hasMethod)
                {
                    continue;
                }

                if (hasMethod)
                {
                    string? method = methodElement.GetString();
                    if (string.IsNullOrWhiteSpace(method))
                    {
                        continue;
                    }

                    JsonElement? parameters = root.TryGetProperty("params", out JsonElement paramsElement)
                        ? paramsElement.Clone()
                        : null;

                    if (hasId)
                    {
                        await HandleRequestAsync(method, idElement, parameters, ct).ConfigureAwait(false);
                        continue;
                    }

                    NotificationReceived?.Invoke(method, parameters);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            Disconnected?.Invoke(error);
        }
    }

    private async Task HandleRequestAsync(string method, JsonElement idElement, JsonElement? parameters, CancellationToken ct)
    {
        if (!TryGetIdValue(idElement, out object? idValue) || idValue is null)
        {
            return;
        }

        McpRequestContext context = new(this, sessionToken: null);
        try
        {
            object? result = await _router.DispatchAsync(method, context, parameters, ct).ConfigureAwait(false);
            await SendResultAsync(idValue, result, ct).ConfigureAwait(false);
        }
        catch (McpJsonRpcException ex)
        {
            await SendErrorAsync(idValue, ex.Code, ex.Message, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await SendErrorAsync(idValue, -32000, ex.Message, ct).ConfigureAwait(false);
        }
    }

    private Task SendResultAsync(object idValue, object? result, CancellationToken ct)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            id = idValue,
            result
        };

        return SendAsync(payload, ct);
    }

    private Task SendErrorAsync(object idValue, int code, string message, CancellationToken ct)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            id = idValue,
            error = new
            {
                code,
                message
            }
        };

        return SendAsync(payload, ct);
    }

    private static bool TryGetIdValue(JsonElement element, out object? id)
    {
        id = null;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long numberId))
                {
                    id = numberId;
                    return true;
                }
                return false;
            case JsonValueKind.String:
                id = element.GetString();
                return id is not null;
            default:
                return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_ownsStreams)
        {
            if (!ReferenceEquals(_input, _output))
            {
                _output.Dispose();
            }

            _input.Dispose();
        }

        _cts?.Dispose();
        _sendLock.Dispose();
    }
}
