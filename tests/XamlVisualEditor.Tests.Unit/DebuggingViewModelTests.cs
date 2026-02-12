using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Xunit;
using XamlVisualEditor.Extensions.Debugging;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DebuggingViewModelTests
{
    [Fact]
    public void Breakpoints_Toggle_Adds_And_Removes()
    {
        BreakpointsViewModel vm = new();
        vm.ToggleBreakpoint("/tmp/test.cs", 12, 1);

        Assert.Single(vm.Items);
        Assert.Equal(12, vm.Items[0].Line);

        vm.ToggleBreakpoint("/tmp/test.cs", 12, 1);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task Debugger_Refreshes_CallStack_And_Locals()
    {
        FakeDebugSession session = new();
        FakeDebuggerService service = new(session);
        DebuggerServiceRegistry registry = new();
        registry.Register(new DebuggerServiceRegistration("fake", "Fake Debugger", service), makeDefault: true);
        DebuggerViewModel vm = new(registry);

        await vm.StartAsync(new DebugLaunchOptions
        {
            AdapterPath = "fake",
            ProgramPath = "/tmp/app.dll"
        });

        await vm.RefreshAsync();

        Assert.Single(vm.CallStack.Frames);
        Assert.Equal("Main", vm.CallStack.Frames[0].Name);
        Assert.True(vm.Locals.Items.Count > 0);
    }

    [Fact]
    public async Task Debugger_Clears_Removed_Breakpoints()
    {
        TrackingDebugSession session = new();
        FakeDebuggerService service = new(session);
        DebuggerServiceRegistry registry = new();
        registry.Register(new DebuggerServiceRegistration("fake", "Fake Debugger", service), makeDefault: true);
        DebuggerViewModel vm = new(registry);

        await vm.StartAsync(new DebugLaunchOptions
        {
            AdapterPath = "fake",
            ProgramPath = "/tmp/app.dll"
        });

        vm.Breakpoints.ToggleBreakpoint("/tmp/test.cs", 12, 1);
        await session.WaitForBreakpointsAsync(expectedCount: 1);

        vm.Breakpoints.ToggleBreakpoint("/tmp/test.cs", 12, 1);
        await session.WaitForBreakpointsAsync(expectedCount: 0);
    }

    private sealed class FakeDebuggerService : IDebuggerService
    {
        private readonly IDebugSession _session;

        public FakeDebuggerService(IDebugSession session)
        {
            _session = session;
        }

        public Task<IDebugSession> LaunchAsync(DebugLaunchOptions options, CancellationToken ct = default)
        {
            return Task.FromResult<IDebugSession>(_session);
        }

        public Task<IDebugSession> AttachAsync(DebugAttachOptions options, CancellationToken ct = default)
        {
            return Task.FromResult<IDebugSession>(_session);
        }
    }

    private sealed class FakeDebugSession : IDebugSession
    {
        public DebugSessionState State => DebugSessionState.Running;

#pragma warning disable 0067
        public event Action<DebugSessionState>? StateChanged;
        public event Action<DebugEvent>? EventReceived;
#pragma warning restore 0067

        public Task<IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(
            string filePath,
            IReadOnlyList<SourceBreakpoint> breakpoints,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<BreakpointInfo>>(breakpoints.Select(bp => new BreakpointInfo
            {
                IsVerified = true,
                Line = bp.Line
            }).ToList());
        }

        public Task<IReadOnlyList<ThreadInfo>> GetThreadsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ThreadInfo>>(new List<ThreadInfo>
            {
                new() { Id = 1, Name = "Main" }
            });
        }

        public Task<StackTraceInfo> GetStackTraceAsync(int threadId, int startFrame, int levels, CancellationToken ct = default)
        {
            StackTraceInfo info = new()
            {
                Frames = new List<StackFrameInfo>
                {
                    new() { Id = 1, Name = "Main", FilePath = "/tmp/test.cs", Line = 10, Column = 1 }
                },
                TotalFrames = 1
            };
            return Task.FromResult(info);
        }

        public Task<IReadOnlyList<ScopeInfo>> GetScopesAsync(int frameId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ScopeInfo>>(new List<ScopeInfo>
            {
                new() { VariablesReference = 1, Name = "Locals", IsExpensive = false }
            });
        }

        public Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(int variablesReference, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<VariableInfo>>(new List<VariableInfo>
            {
                new() { Name = "value", Value = "42", TypeName = "int", VariablesReference = 0 }
            });
        }

        public Task<EvaluateResult> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new EvaluateResult { Result = "42", TypeName = "int", VariablesReference = 0 });
        }

        public Task ContinueAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepInAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepOutAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task StepOverAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task PauseAsync(int? threadId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(bool terminateDebuggee, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TrackingDebugSession : IDebugSession
    {
        private readonly TaskCompletionSource<int> _firstNonEmpty = new();
        private readonly TaskCompletionSource<int> _firstEmpty = new();

        public DebugSessionState State => DebugSessionState.Running;

#pragma warning disable 0067
        public event Action<DebugSessionState>? StateChanged;
        public event Action<DebugEvent>? EventReceived;
#pragma warning restore 0067

        public Task WaitForBreakpointsAsync(int expectedCount)
        {
            return expectedCount == 0 ? _firstEmpty.Task : _firstNonEmpty.Task;
        }

        public Task<IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(
            string filePath,
            IReadOnlyList<SourceBreakpoint> breakpoints,
            CancellationToken ct = default)
        {
            if (breakpoints.Count == 0)
            {
                _firstEmpty.TrySetResult(0);
            }
            else
            {
                _firstNonEmpty.TrySetResult(breakpoints.Count);
            }

            return Task.FromResult<IReadOnlyList<BreakpointInfo>>(breakpoints.Select(bp => new BreakpointInfo
            {
                IsVerified = true,
                Line = bp.Line
            }).ToList());
        }

        public Task<IReadOnlyList<ThreadInfo>> GetThreadsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ThreadInfo>>(new List<ThreadInfo>
            {
                new() { Id = 1, Name = "Main" }
            });
        }

        public Task<StackTraceInfo> GetStackTraceAsync(int threadId, int startFrame, int levels, CancellationToken ct = default)
        {
            StackTraceInfo info = new()
            {
                Frames = new List<StackFrameInfo>
                {
                    new() { Id = 1, Name = "Main", FilePath = "/tmp/test.cs", Line = 10, Column = 1 }
                },
                TotalFrames = 1
            };
            return Task.FromResult(info);
        }

        public Task<IReadOnlyList<ScopeInfo>> GetScopesAsync(int frameId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ScopeInfo>>(new List<ScopeInfo>
            {
                new() { VariablesReference = 1, Name = "Locals", IsExpensive = false }
            });
        }

        public Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(int variablesReference, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<VariableInfo>>(new List<VariableInfo>
            {
                new() { Name = "value", Value = "42", TypeName = "int", VariablesReference = 0 }
            });
        }

        public Task<EvaluateResult> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new EvaluateResult { Result = "42", TypeName = "int", VariablesReference = 0 });
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
