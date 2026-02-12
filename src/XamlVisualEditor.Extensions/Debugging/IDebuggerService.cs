using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions.Debugging;

public interface IDebuggerService
{
    Task<IDebugSession> LaunchAsync(DebugLaunchOptions options, CancellationToken ct = default);
    Task<IDebugSession> AttachAsync(DebugAttachOptions options, CancellationToken ct = default);
}

public interface IDebugSession : IAsyncDisposable
{
    DebugSessionState State { get; }

    event Action<DebugSessionState>? StateChanged;
    event Action<DebugEvent>? EventReceived;

    Task<IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(
        string filePath,
        IReadOnlyList<SourceBreakpoint> breakpoints,
        CancellationToken ct = default);

    Task<IReadOnlyList<ThreadInfo>> GetThreadsAsync(CancellationToken ct = default);
    Task<StackTraceInfo> GetStackTraceAsync(int threadId, int startFrame, int levels, CancellationToken ct = default);
    Task<IReadOnlyList<ScopeInfo>> GetScopesAsync(int frameId, CancellationToken ct = default);
    Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(int variablesReference, CancellationToken ct = default);
    Task<EvaluateResult> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default);

    Task ContinueAsync(int? threadId = null, CancellationToken ct = default);
    Task StepInAsync(int? threadId = null, CancellationToken ct = default);
    Task StepOutAsync(int? threadId = null, CancellationToken ct = default);
    Task StepOverAsync(int? threadId = null, CancellationToken ct = default);
    Task PauseAsync(int? threadId = null, CancellationToken ct = default);
    Task DisconnectAsync(bool terminateDebuggee, CancellationToken ct = default);
}
