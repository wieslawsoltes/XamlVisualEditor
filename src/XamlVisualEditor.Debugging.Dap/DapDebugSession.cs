using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core.Debugging;

namespace XamlVisualEditor.Debugging.Dap;

internal sealed class DapDebugSession : IDebugSession
{
    private readonly DapDebugAdapterHost _host;
    private readonly DapProtocolClient _client;
    private DebugSessionState _state;

    public DapDebugSession(DapDebugAdapterHost host)
    {
        _host = host;
        _client = host.Client;
        _client.EventReceived += OnEventReceived;
        _state = DebugSessionState.Created;
    }

    public DebugSessionState State => _state;

    public event Action<DebugSessionState>? StateChanged;
    public event Action<DebugEvent>? EventReceived;

    public async Task InitializeAsync(CancellationToken ct)
    {
        SetState(DebugSessionState.Initializing);
        var args = new
        {
            clientID = "XamlVisualEditor",
            adapterID = "netcoredbg",
            linesStartAt1 = true,
            columnsStartAt1 = true,
            pathFormat = "path",
            supportsVariableType = true,
            supportsVariablePaging = false,
            supportsRunInTerminalRequest = false
        };

        await _client.SendRequestAsync("initialize", args, ct).ConfigureAwait(false);
    }

    public async Task LaunchAsync(DebugLaunchOptions options, CancellationToken ct)
    {
        var args = new
        {
            program = options.ProgramPath,
            args = options.Arguments is null ? Array.Empty<string>() : SplitArguments(options.Arguments),
            cwd = options.WorkingDirectory,
            env = options.Environment,
            stopAtEntry = options.StopAtEntry
        };

        await _client.SendRequestAsync("launch", args, ct).ConfigureAwait(false);
        await _client.SendRequestAsync("configurationDone", null, ct).ConfigureAwait(false);
        SetState(DebugSessionState.Running);
    }

    public async Task AttachAsync(DebugAttachOptions options, CancellationToken ct)
    {
        var args = new
        {
            processId = options.ProcessId
        };

        await _client.SendRequestAsync("attach", args, ct).ConfigureAwait(false);
        await _client.SendRequestAsync("configurationDone", null, ct).ConfigureAwait(false);
        SetState(DebugSessionState.Running);
    }

    public async Task<IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(
        string filePath,
        IReadOnlyList<SourceBreakpoint> breakpoints,
        CancellationToken ct = default)
    {
        var args = new
        {
            source = new { path = filePath },
            breakpoints = breakpoints.Select(bp => new
            {
                line = bp.Line,
                column = bp.Column,
                condition = bp.Condition,
                hitCondition = bp.HitCondition,
                logMessage = bp.LogMessage
            }).ToArray()
        };

        JsonElement body = await _client.SendRequestAsync("setBreakpoints", args, ct).ConfigureAwait(false);
        if (!body.TryGetProperty("breakpoints", out JsonElement itemsElement))
        {
            return Array.Empty<BreakpointInfo>();
        }

        List<BreakpointInfo> results = new();
        foreach (JsonElement item in itemsElement.EnumerateArray())
        {
            results.Add(new BreakpointInfo
            {
                IsVerified = item.TryGetProperty("verified", out JsonElement verified) && verified.GetBoolean(),
                Line = item.TryGetProperty("line", out JsonElement line) ? line.GetInt32() : 0,
                Column = item.TryGetProperty("column", out JsonElement column) ? column.GetInt32() : null,
                Message = item.TryGetProperty("message", out JsonElement message) ? message.GetString() : null,
                Id = item.TryGetProperty("id", out JsonElement id) ? id.GetInt32() : null
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<ThreadInfo>> GetThreadsAsync(CancellationToken ct = default)
    {
        JsonElement body = await _client.SendRequestAsync("threads", null, ct).ConfigureAwait(false);
        if (!body.TryGetProperty("threads", out JsonElement itemsElement))
        {
            return Array.Empty<ThreadInfo>();
        }

        List<ThreadInfo> threads = new();
        foreach (JsonElement item in itemsElement.EnumerateArray())
        {
            threads.Add(new ThreadInfo
            {
                Id = item.GetProperty("id").GetInt32(),
                Name = item.GetProperty("name").GetString() ?? "Thread"
            });
        }

        return threads;
    }

    public async Task<StackTraceInfo> GetStackTraceAsync(int threadId, int startFrame, int levels, CancellationToken ct = default)
    {
        var args = new
        {
            threadId,
            startFrame,
            levels
        };

        JsonElement body = await _client.SendRequestAsync("stackTrace", args, ct).ConfigureAwait(false);
        List<StackFrameInfo> frames = new();
        if (body.TryGetProperty("stackFrames", out JsonElement itemsElement))
        {
            foreach (JsonElement item in itemsElement.EnumerateArray())
            {
                frames.Add(new StackFrameInfo
                {
                    Id = item.GetProperty("id").GetInt32(),
                    Name = item.GetProperty("name").GetString() ?? "Frame",
                    FilePath = item.TryGetProperty("source", out JsonElement source)
                        && source.TryGetProperty("path", out JsonElement path)
                            ? path.GetString()
                            : null,
                    Line = item.TryGetProperty("line", out JsonElement line) ? line.GetInt32() : null,
                    Column = item.TryGetProperty("column", out JsonElement column) ? column.GetInt32() : null,
                    ModuleName = item.TryGetProperty("moduleId", out JsonElement moduleId) ? moduleId.ToString() : null
                });
            }
        }

        int? total = body.TryGetProperty("totalFrames", out JsonElement totalElement)
            ? totalElement.GetInt32()
            : null;

        return new StackTraceInfo
        {
            Frames = frames,
            TotalFrames = total
        };
    }

    public async Task<IReadOnlyList<ScopeInfo>> GetScopesAsync(int frameId, CancellationToken ct = default)
    {
        var args = new { frameId };
        JsonElement body = await _client.SendRequestAsync("scopes", args, ct).ConfigureAwait(false);
        if (!body.TryGetProperty("scopes", out JsonElement itemsElement))
        {
            return Array.Empty<ScopeInfo>();
        }

        List<ScopeInfo> scopes = new();
        foreach (JsonElement item in itemsElement.EnumerateArray())
        {
            scopes.Add(new ScopeInfo
            {
                VariablesReference = item.GetProperty("variablesReference").GetInt32(),
                Name = item.GetProperty("name").GetString() ?? "Scope",
                IsExpensive = item.TryGetProperty("expensive", out JsonElement expensive) && expensive.GetBoolean()
            });
        }

        return scopes;
    }

    public async Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(int variablesReference, CancellationToken ct = default)
    {
        var args = new { variablesReference };
        JsonElement body = await _client.SendRequestAsync("variables", args, ct).ConfigureAwait(false);
        if (!body.TryGetProperty("variables", out JsonElement itemsElement))
        {
            return Array.Empty<VariableInfo>();
        }

        List<VariableInfo> variables = new();
        foreach (JsonElement item in itemsElement.EnumerateArray())
        {
            variables.Add(new VariableInfo
            {
                Name = item.GetProperty("name").GetString() ?? "var",
                Value = item.GetProperty("value").GetString() ?? string.Empty,
                TypeName = item.TryGetProperty("type", out JsonElement type) ? type.GetString() ?? string.Empty : string.Empty,
                VariablesReference = item.TryGetProperty("variablesReference", out JsonElement vr) ? vr.GetInt32() : 0
            });
        }

        return variables;
    }

    public async Task<EvaluateResult> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default)
    {
        var args = new
        {
            expression = request.Expression,
            frameId = request.FrameId
        };

        JsonElement body = await _client.SendRequestAsync("evaluate", args, ct).ConfigureAwait(false);
        return new EvaluateResult
        {
            Result = body.TryGetProperty("result", out JsonElement result) ? result.GetString() ?? string.Empty : string.Empty,
            TypeName = body.TryGetProperty("type", out JsonElement type) ? type.GetString() : null,
            VariablesReference = body.TryGetProperty("variablesReference", out JsonElement vr) ? vr.GetInt32() : 0
        };
    }

    public Task ContinueAsync(int? threadId = null, CancellationToken ct = default)
    {
        return _client.SendRequestAsync("continue", new { threadId }, ct);
    }

    public Task StepInAsync(int? threadId = null, CancellationToken ct = default)
    {
        return _client.SendRequestAsync("stepIn", new { threadId }, ct);
    }

    public Task StepOutAsync(int? threadId = null, CancellationToken ct = default)
    {
        return _client.SendRequestAsync("stepOut", new { threadId }, ct);
    }

    public Task StepOverAsync(int? threadId = null, CancellationToken ct = default)
    {
        return _client.SendRequestAsync("next", new { threadId }, ct);
    }

    public Task PauseAsync(int? threadId = null, CancellationToken ct = default)
    {
        return _client.SendRequestAsync("pause", new { threadId }, ct);
    }

    public Task DisconnectAsync(bool terminateDebuggee, CancellationToken ct = default)
    {
        var args = new { terminateDebuggee };
        return _client.SendRequestAsync("disconnect", args, ct);
    }

    private void OnEventReceived(string name, JsonElement body)
    {
        switch (name)
        {
            case "output":
                HandleOutput(body);
                break;
            case "stopped":
                HandleStopped(body);
                break;
            case "continued":
                HandleContinued(body);
                break;
            case "thread":
                HandleThread(body);
                break;
            case "breakpoint":
                HandleBreakpoint(body);
                break;
            case "terminated":
                SetState(DebugSessionState.Terminated);
                EventReceived?.Invoke(new DebugTerminatedEvent(false));
                break;
            case "exited":
                SetState(DebugSessionState.Terminated);
                EventReceived?.Invoke(new DebugTerminatedEvent(false));
                break;
        }
    }

    private void HandleOutput(JsonElement body)
    {
        string categoryText = body.TryGetProperty("category", out JsonElement category)
            ? category.GetString() ?? string.Empty
            : string.Empty;

        DebugOutputCategory categoryValue = categoryText switch
        {
            "stdout" => DebugOutputCategory.StdOut,
            "stderr" => DebugOutputCategory.StdErr,
            "telemetry" => DebugOutputCategory.Telemetry,
            _ => DebugOutputCategory.Console
        };

        string text = body.TryGetProperty("output", out JsonElement output)
            ? output.GetString() ?? string.Empty
            : string.Empty;

        if (text.Length == 0)
        {
            return;
        }

        EventReceived?.Invoke(new DebugOutputEvent(categoryValue, text));
    }

    private void HandleStopped(JsonElement body)
    {
        string reasonText = body.TryGetProperty("reason", out JsonElement reason) ? reason.GetString() ?? string.Empty : string.Empty;
        DebugStopReason reasonValue = reasonText switch
        {
            "breakpoint" => DebugStopReason.Breakpoint,
            "step" => DebugStopReason.Step,
            "exception" => DebugStopReason.Exception,
            "pause" => DebugStopReason.Pause,
            "entry" => DebugStopReason.Entry,
            _ => DebugStopReason.Unknown
        };

        int? threadId = body.TryGetProperty("threadId", out JsonElement threadIdElement)
            ? threadIdElement.GetInt32()
            : null;
        string? description = body.TryGetProperty("description", out JsonElement descriptionElement)
            ? descriptionElement.GetString()
            : null;

        SetState(DebugSessionState.Paused);
        EventReceived?.Invoke(new DebugStoppedEvent(reasonValue, threadId, description));
    }

    private void HandleContinued(JsonElement body)
    {
        int? threadId = body.TryGetProperty("threadId", out JsonElement threadIdElement)
            ? threadIdElement.GetInt32()
            : null;

        SetState(DebugSessionState.Running);
        EventReceived?.Invoke(new DebugContinuedEvent(threadId));
    }

    private void HandleThread(JsonElement body)
    {
        string? reason = body.TryGetProperty("reason", out JsonElement reasonElement)
            ? reasonElement.GetString()
            : null;
        int threadId = body.TryGetProperty("threadId", out JsonElement threadIdElement)
            ? threadIdElement.GetInt32()
            : 0;
        bool started = string.Equals(reason, "started", StringComparison.OrdinalIgnoreCase);
        EventReceived?.Invoke(new DebugThreadEvent(threadId, $"Thread {threadId}", started));
    }

    private void HandleBreakpoint(JsonElement body)
    {
        if (!body.TryGetProperty("breakpoint", out JsonElement breakpointElement))
        {
            return;
        }

        BreakpointInfo breakpoint = new()
        {
            IsVerified = breakpointElement.TryGetProperty("verified", out JsonElement verified) && verified.GetBoolean(),
            Line = breakpointElement.TryGetProperty("line", out JsonElement line) ? line.GetInt32() : 0,
            Column = breakpointElement.TryGetProperty("column", out JsonElement column) ? column.GetInt32() : null,
            Message = breakpointElement.TryGetProperty("message", out JsonElement message) ? message.GetString() : null,
            Id = breakpointElement.TryGetProperty("id", out JsonElement id) ? id.GetInt32() : null
        };

        string? reason = body.TryGetProperty("reason", out JsonElement reasonElement)
            ? reasonElement.GetString()
            : null;
        BreakpointChangeType changeType = reason switch
        {
            "changed" => BreakpointChangeType.Changed,
            "removed" => BreakpointChangeType.Removed,
            _ => BreakpointChangeType.Added
        };

        EventReceived?.Invoke(new DebugBreakpointEvent(breakpoint, changeType));
    }

    private static string[] SplitArguments(string args)
    {
        return args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void SetState(DebugSessionState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        StateChanged?.Invoke(_state);
    }

    public async ValueTask DisposeAsync()
    {
        _client.EventReceived -= OnEventReceived;
        await _host.DisposeAsync().ConfigureAwait(false);
    }
}
