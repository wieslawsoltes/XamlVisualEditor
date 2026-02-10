using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed class AcpTerminalManager
{
    private readonly ConcurrentDictionary<string, TerminalState> _terminals = new(StringComparer.Ordinal);

    public void Register(AcpProtocolClient client)
    {
        client.RegisterRequestHandler("terminal/create", HandleCreateAsync);
        client.RegisterRequestHandler("terminal/output", HandleOutputAsync);
        client.RegisterRequestHandler("terminal/kill", HandleKillAsync);
        client.RegisterRequestHandler("terminal/release", HandleReleaseAsync);
        client.RegisterRequestHandler("terminal/wait_for_exit", HandleWaitForExitAsync);
    }

    public void Unregister(AcpProtocolClient client)
    {
        client.TryRemoveRequestHandler("terminal/create");
        client.TryRemoveRequestHandler("terminal/output");
        client.TryRemoveRequestHandler("terminal/kill");
        client.TryRemoveRequestHandler("terminal/release");
        client.TryRemoveRequestHandler("terminal/wait_for_exit");
    }

    public async Task ReleaseAllAsync()
    {
        foreach (KeyValuePair<string, TerminalState> pair in _terminals)
        {
            try
            {
                await ReleaseTerminalAsync(pair.Key).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async Task<JsonElement?> HandleCreateAsync(JsonElement? parameters, CancellationToken ct)
    {
        string command = RequireString(parameters, "command");
        string? cwd = GetOptionalString(parameters, "cwd");
        int? outputByteLimit = GetOptionalNullableInt(parameters, "outputByteLimit");

        string[] args = GetOptionalStringArray(parameters, "args");
        Dictionary<string, string> env = GetOptionalEnv(parameters);

        ProcessStartInfo startInfo = new()
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        if (args.Length > 0)
        {
            startInfo.ArgumentList.Clear();
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            if (!Path.IsPathRooted(cwd))
            {
                throw new JsonRpcException(-32602, "Working directory must be absolute.");
            }

            startInfo.WorkingDirectory = cwd;
        }

        foreach (KeyValuePair<string, string> pair in env)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new JsonRpcException(-32000, "Failed to start terminal command.");
        }

        if (outputByteLimit is not null && outputByteLimit < 0)
        {
            outputByteLimit = 0;
        }

        string terminalId = Guid.NewGuid().ToString("N");
        TerminalState state = TerminalState.Create(terminalId, process, outputByteLimit);
        if (!_terminals.TryAdd(terminalId, state))
        {
            await state.DisposeAsync().ConfigureAwait(false);
            throw new JsonRpcException(-32000, "Failed to register terminal.");
        }

        return JsonSerializer.SerializeToElement(new { terminalId });
    }

    private Task<JsonElement?> HandleOutputAsync(JsonElement? parameters, CancellationToken ct)
    {
        string terminalId = RequireString(parameters, "terminalId");
        TerminalState state = GetTerminal(terminalId);

        (string output, bool truncated) = state.Output.GetSnapshot();
        TerminalExitStatus? exitStatus = state.GetExitStatusIfCompleted();

        return Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(new
        {
            output,
            truncated,
            exitStatus
        }));
    }

    private Task<JsonElement?> HandleKillAsync(JsonElement? parameters, CancellationToken ct)
    {
        string terminalId = RequireString(parameters, "terminalId");
        TerminalState state = GetTerminal(terminalId);
        state.Kill("SIGKILL");
        return Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(new { }));
    }

    private async Task<JsonElement?> HandleReleaseAsync(JsonElement? parameters, CancellationToken ct)
    {
        string terminalId = RequireString(parameters, "terminalId");
        await ReleaseTerminalAsync(terminalId).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new { });
    }

    private async Task<JsonElement?> HandleWaitForExitAsync(JsonElement? parameters, CancellationToken ct)
    {
        string terminalId = RequireString(parameters, "terminalId");
        TerminalState state = GetTerminal(terminalId);
        TerminalExitStatus exitStatus = await state.WaitForExitAsync().ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new { exitCode = exitStatus.ExitCode, signal = exitStatus.Signal });
    }

    private TerminalState GetTerminal(string terminalId)
    {
        if (!_terminals.TryGetValue(terminalId, out TerminalState? state))
        {
            throw new JsonRpcException(-32002, "Terminal not found.");
        }

        return state;
    }

    private async Task ReleaseTerminalAsync(string terminalId)
    {
        if (!_terminals.TryRemove(terminalId, out TerminalState? state))
        {
            throw new JsonRpcException(-32002, "Terminal not found.");
        }

        state.Kill("SIGKILL");
        await state.DisposeAsync().ConfigureAwait(false);
    }

    private static string RequireString(JsonElement? parameters, string name)
    {
        if (parameters is null)
        {
            throw new JsonRpcException(-32602, "Missing parameters.");
        }

        if (parameters.Value.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new JsonRpcException(-32602, $"Missing parameter '{name}'.");
    }

    private static string? GetOptionalString(JsonElement? parameters, string name)
    {
        if (parameters is null)
        {
            return null;
        }

        if (parameters.Value.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return null;
    }

    private static int? GetOptionalNullableInt(JsonElement? parameters, string name)
    {
        if (parameters is null)
        {
            return null;
        }

        if (parameters.Value.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out int value))
        {
            return value;
        }

        return null;
    }

    private static string[] GetOptionalStringArray(JsonElement? parameters, string name)
    {
        if (parameters is null)
        {
            return Array.Empty<string>();
        }

        if (!parameters.Value.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        List<string> results = new();
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? value = item.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    results.Add(value);
                }
            }
        }

        return results.ToArray();
    }

    private static Dictionary<string, string> GetOptionalEnv(JsonElement? parameters)
    {
        Dictionary<string, string> env = new(StringComparer.Ordinal);
        if (parameters is null)
        {
            return env;
        }

        if (!parameters.Value.TryGetProperty("env", out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            return env;
        }

        foreach (JsonElement entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!entry.TryGetProperty("name", out JsonElement nameElement) || nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (!entry.TryGetProperty("value", out JsonElement valueElement) || valueElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string? name = nameElement.GetString();
            string? value = valueElement.GetString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                env[name] = value ?? string.Empty;
            }
        }

        return env;
    }

    private sealed class TerminalState : IAsyncDisposable
    {
        private readonly CancellationTokenSource _outputCts = new();
        private string? _exitSignal;

        private TerminalState(string id, Process process, OutputBuffer output)
        {
            Id = id;
            Process = process;
            Output = output;
            StdOutTask = PumpAsync(process.StandardOutput, output, _outputCts.Token);
            StdErrTask = PumpAsync(process.StandardError, output, _outputCts.Token);
            ExitTask = WaitForExitInternalAsync();
        }

        public string Id { get; }
        public Process Process { get; }
        public OutputBuffer Output { get; }
        public Task StdOutTask { get; }
        public Task StdErrTask { get; }
        public Task<TerminalExitStatus> ExitTask { get; }

        public static TerminalState Create(string id, Process process, int? outputByteLimit)
        {
            OutputBuffer output = new(outputByteLimit);
            return new TerminalState(id, process, output);
        }

        public void Kill(string signal)
        {
            if (_exitSignal is null)
            {
                _exitSignal = signal;
            }

            if (!Process.HasExited)
            {
                try
                {
                    Process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            }
        }

        public TerminalExitStatus? GetExitStatusIfCompleted()
        {
            if (_exitSignal is not null)
            {
                return new TerminalExitStatus { ExitCode = null, Signal = _exitSignal };
            }

            if (!Process.HasExited)
            {
                return null;
            }

            return new TerminalExitStatus { ExitCode = Process.ExitCode, Signal = null };
        }

        public Task<TerminalExitStatus> WaitForExitAsync()
        {
            return ExitTask;
        }

        private async Task<TerminalExitStatus> WaitForExitInternalAsync()
        {
            try
            {
                await Process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            if (_exitSignal is not null)
            {
                return new TerminalExitStatus { ExitCode = null, Signal = _exitSignal };
            }

            return new TerminalExitStatus { ExitCode = Process.HasExited ? Process.ExitCode : null, Signal = null };
        }

        private static async Task PumpAsync(StreamReader reader, OutputBuffer output, CancellationToken ct)
        {
            char[] buffer = new char[4096];
            while (!ct.IsCancellationRequested)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                output.Append(new string(buffer, 0, read));
            }
        }

        public async ValueTask DisposeAsync()
        {
            _outputCts.Cancel();

            try
            {
                await Task.WhenAll(StdOutTask, StdErrTask).ConfigureAwait(false);
            }
            catch
            {
            }

            Process.Dispose();
            _outputCts.Dispose();
        }
    }

    private sealed class OutputBuffer
    {
        private readonly object _gate = new();
        private readonly List<OutputChunk> _chunks = new();
        private readonly int? _byteLimit;
        private int _totalBytes;
        private bool _truncated;

        public OutputBuffer(int? byteLimit)
        {
            _byteLimit = byteLimit;
        }

        public void Append(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int bytes = Encoding.UTF8.GetByteCount(text);
            lock (_gate)
            {
                if (_byteLimit == 0)
                {
                    _truncated = true;
                    return;
                }

                _chunks.Add(new OutputChunk(text, bytes));
                _totalBytes += bytes;

                if (_byteLimit is not null)
                {
                    EnforceLimit(_byteLimit.Value);
                }
            }
        }

        public (string Output, bool Truncated) GetSnapshot()
        {
            lock (_gate)
            {
                if (_chunks.Count == 0)
                {
                    return (string.Empty, _truncated);
                }

                StringBuilder builder = new();
                foreach (OutputChunk chunk in _chunks)
                {
                    builder.Append(chunk.Text);
                }

                return (builder.ToString(), _truncated);
            }
        }

        private void EnforceLimit(int limit)
        {
            if (limit < 0)
            {
                return;
            }

            while (_totalBytes > limit && _chunks.Count > 0)
            {
                int overflow = _totalBytes - limit;
                OutputChunk chunk = _chunks[0];

                if (chunk.Bytes <= overflow)
                {
                    _chunks.RemoveAt(0);
                    _totalBytes -= chunk.Bytes;
                    _truncated = true;
                    continue;
                }

                TrimLeadingBytes(chunk, overflow, out OutputChunk trimmed, out int removedBytes);
                _chunks[0] = trimmed;
                _totalBytes -= removedBytes;
                _truncated = true;
            }
        }

        private static void TrimLeadingBytes(OutputChunk chunk, int bytesToRemove, out OutputChunk trimmed, out int removedBytes)
        {
            string text = chunk.Text;
            int index = 0;
            int removed = 0;
            ReadOnlySpan<char> span = text.AsSpan();

            while (index < span.Length && removed < bytesToRemove)
            {
                if (System.Text.Rune.DecodeFromUtf16(span.Slice(index), out System.Text.Rune rune, out int charsConsumed)
                    != OperationStatus.Done)
                {
                    break;
                }

                removed += rune.Utf8SequenceLength;
                index += charsConsumed;
            }

            string newText = index >= text.Length ? string.Empty : text[index..];
            int newBytes = Encoding.UTF8.GetByteCount(newText);
            trimmed = new OutputChunk(newText, newBytes);
            removedBytes = removed;
        }
    }

    private readonly record struct OutputChunk(string Text, int Bytes);

    private sealed class TerminalExitStatus
    {
        public int? ExitCode { get; set; }
        public string? Signal { get; set; }
    }
}
