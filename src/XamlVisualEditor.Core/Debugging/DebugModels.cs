using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Core.Debugging;

public enum DebugSessionState
{
    Created,
    Initializing,
    Running,
    Paused,
    Stopped,
    Terminated,
    Failed
}

public enum DebugStopReason
{
    Unknown,
    Breakpoint,
    Step,
    Exception,
    Pause,
    Entry
}

public enum DebugOutputCategory
{
    Console,
    StdOut,
    StdErr,
    Telemetry
}

public enum BreakpointChangeType
{
    Added,
    Changed,
    Removed
}

public sealed class DebugLaunchOptions
{
    public required string AdapterPath { get; init; }
    public required string ProgramPath { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
    public bool StopAtEntry { get; init; }
}

public sealed class DebugAttachOptions
{
    public required string AdapterPath { get; init; }
    public required int ProcessId { get; init; }
}

public sealed class SourceBreakpoint
{
    public required int Line { get; init; }
    public int? Column { get; init; }
    public string? Condition { get; init; }
    public string? HitCondition { get; init; }
    public string? LogMessage { get; init; }
}

public sealed class BreakpointInfo
{
    public required bool IsVerified { get; init; }
    public required int Line { get; init; }
    public int? Column { get; init; }
    public string? Message { get; init; }
    public int? Id { get; init; }
}

public sealed class ThreadInfo
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}

public sealed class StackFrameInfo
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
    public string? ModuleName { get; init; }
}

public sealed class StackTraceInfo
{
    public required IReadOnlyList<StackFrameInfo> Frames { get; init; }
    public int? TotalFrames { get; init; }
}

public sealed class ScopeInfo
{
    public required int VariablesReference { get; init; }
    public required string Name { get; init; }
    public bool IsExpensive { get; init; }
}

public sealed class VariableInfo
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public required string TypeName { get; init; }
    public int VariablesReference { get; init; }
}

public sealed class EvaluateRequest
{
    public required string Expression { get; init; }
    public int? FrameId { get; init; }
}

public sealed class EvaluateResult
{
    public required string Result { get; init; }
    public string? TypeName { get; init; }
    public int VariablesReference { get; init; }
}

public abstract class DebugEvent
{
    protected DebugEvent(DebugEventKind kind)
    {
        Kind = kind;
    }

    public DebugEventKind Kind { get; }
}

public enum DebugEventKind
{
    Output,
    Stopped,
    Continued,
    Thread,
    Breakpoint,
    Terminated
}

public sealed class DebugOutputEvent : DebugEvent
{
    public DebugOutputEvent(DebugOutputCategory category, string text)
        : base(DebugEventKind.Output)
    {
        Category = category;
        Text = text;
    }

    public DebugOutputCategory Category { get; }
    public string Text { get; }
}

public sealed class DebugStoppedEvent : DebugEvent
{
    public DebugStoppedEvent(DebugStopReason reason, int? threadId, string? description)
        : base(DebugEventKind.Stopped)
    {
        Reason = reason;
        ThreadId = threadId;
        Description = description;
    }

    public DebugStopReason Reason { get; }
    public int? ThreadId { get; }
    public string? Description { get; }
}

public sealed class DebugContinuedEvent : DebugEvent
{
    public DebugContinuedEvent(int? threadId)
        : base(DebugEventKind.Continued)
    {
        ThreadId = threadId;
    }

    public int? ThreadId { get; }
}

public sealed class DebugThreadEvent : DebugEvent
{
    public DebugThreadEvent(int threadId, string name, bool isStarted)
        : base(DebugEventKind.Thread)
    {
        ThreadId = threadId;
        Name = name;
        IsStarted = isStarted;
    }

    public int ThreadId { get; }
    public string Name { get; }
    public bool IsStarted { get; }
}

public sealed class DebugBreakpointEvent : DebugEvent
{
    public DebugBreakpointEvent(BreakpointInfo breakpoint, BreakpointChangeType changeType)
        : base(DebugEventKind.Breakpoint)
    {
        Breakpoint = breakpoint;
        ChangeType = changeType;
    }

    public BreakpointInfo Breakpoint { get; }
    public BreakpointChangeType ChangeType { get; }
}

public sealed class DebugTerminatedEvent : DebugEvent
{
    public DebugTerminatedEvent(bool isRestarted)
        : base(DebugEventKind.Terminated)
    {
        IsRestarted = isRestarted;
    }

    public bool IsRestarted { get; }
}
