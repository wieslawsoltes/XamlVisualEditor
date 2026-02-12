using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions.Debugging;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class DebuggerServiceRegistryTests
{
    [Fact]
    public void Registry_Tracks_Active_Service()
    {
        DebuggerServiceRegistry registry = new();
        FakeDebuggerService first = new();
        FakeDebuggerService second = new();

        registry.Register(new DebuggerServiceRegistration("first", "First", first), makeDefault: true);
        registry.Register(new DebuggerServiceRegistration("second", "Second", second));

        Assert.Equal("first", registry.ActiveServiceId);
        Assert.Same(first, registry.GetActiveService());

        registry.ActiveServiceId = "second";

        Assert.Equal("second", registry.ActiveServiceId);
        Assert.Same(second, registry.GetActiveService());
    }

    [Fact]
    public void Registry_Ignores_Unknown_Active_Service()
    {
        DebuggerServiceRegistry registry = new();
        FakeDebuggerService first = new();

        registry.Register(new DebuggerServiceRegistration("first", "First", first), makeDefault: true);
        registry.ActiveServiceId = "missing";

        Assert.Equal("first", registry.ActiveServiceId);
    }

    private sealed class FakeDebuggerService : IDebuggerService
    {
        public Task<IDebugSession> LaunchAsync(DebugLaunchOptions options, CancellationToken ct = default)
        {
            return Task.FromResult<IDebugSession>(new FakeDebugSession());
        }

        public Task<IDebugSession> AttachAsync(DebugAttachOptions options, CancellationToken ct = default)
        {
            return Task.FromResult<IDebugSession>(new FakeDebugSession());
        }
    }

    private sealed class FakeDebugSession : IDebugSession
    {
        public DebugSessionState State => DebugSessionState.Created;

#pragma warning disable 0067
        public event System.Action<DebugSessionState>? StateChanged;
        public event System.Action<DebugEvent>? EventReceived;
#pragma warning restore 0067

        public Task<System.Collections.Generic.IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(
            string filePath,
            System.Collections.Generic.IReadOnlyList<SourceBreakpoint> breakpoints,
            CancellationToken ct = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<BreakpointInfo>>(
                System.Array.Empty<BreakpointInfo>());
        }

        public Task<System.Collections.Generic.IReadOnlyList<ThreadInfo>> GetThreadsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<ThreadInfo>>(
                System.Array.Empty<ThreadInfo>());
        }

        public Task<StackTraceInfo> GetStackTraceAsync(int threadId, int startFrame, int levels, CancellationToken ct = default)
        {
            return Task.FromResult(new StackTraceInfo { Frames = System.Array.Empty<StackFrameInfo>() });
        }

        public Task<System.Collections.Generic.IReadOnlyList<ScopeInfo>> GetScopesAsync(int frameId, CancellationToken ct = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<ScopeInfo>>(
                System.Array.Empty<ScopeInfo>());
        }

        public Task<System.Collections.Generic.IReadOnlyList<VariableInfo>> GetVariablesAsync(int variablesReference, CancellationToken ct = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<VariableInfo>>(
                System.Array.Empty<VariableInfo>());
        }

        public Task<EvaluateResult> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new EvaluateResult { Result = string.Empty, VariablesReference = 0 });
        }

        public Task ContinueAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepInAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepOutAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepOverAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task PauseAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(bool terminateDebuggee, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
