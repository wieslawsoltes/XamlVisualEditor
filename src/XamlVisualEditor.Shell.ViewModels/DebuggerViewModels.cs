using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core.Debugging;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class BreakpointEntryViewModel : ReactiveObject
{
    public BreakpointEntryViewModel(string filePath, int line, int? column)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
    }

    public string FilePath { get; }

    [Reactive]
    public int Line { get; set; }

    [Reactive]
    public int? Column { get; set; }

    [Reactive]
    public string? Condition { get; set; }

    [Reactive]
    public string? HitCondition { get; set; }

    [Reactive]
    public string? LogMessage { get; set; }

    [Reactive]
    public bool IsEnabled { get; set; } = true;

    [Reactive]
    public bool IsVerified { get; set; }

    [Reactive]
    public string? Message { get; set; }
}

public sealed class BreakpointsViewModel : ReactiveObject
{
    private readonly Dictionary<BreakpointEntryViewModel, IDisposable> _subscriptions = new();

    public ObservableCollection<BreakpointEntryViewModel> Items { get; } = new();

    [Reactive]
    public BreakpointEntryViewModel? SelectedBreakpoint { get; set; }

    public event Action? BreakpointsChanged;

    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<BreakpointEntryViewModel, Unit> RemoveCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveSelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> EnableSelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> DisableSelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearConditionCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearHitConditionCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearLogMessageCommand { get; }

    public BreakpointsViewModel()
    {
        ClearCommand = ReactiveCommand.Create(Clear);
        RemoveCommand = ReactiveCommand.Create<BreakpointEntryViewModel>(Remove);
        RemoveSelectedCommand = ReactiveCommand.Create(RemoveSelected);
        EnableSelectedCommand = ReactiveCommand.Create(() => SetSelectedEnabled(true));
        DisableSelectedCommand = ReactiveCommand.Create(() => SetSelectedEnabled(false));
        ClearConditionCommand = ReactiveCommand.Create(() => UpdateSelectedCondition(null));
        ClearHitConditionCommand = ReactiveCommand.Create(() => UpdateSelectedHitCondition(null));
        ClearLogMessageCommand = ReactiveCommand.Create(() => UpdateSelectedLogMessage(null));
        Items.CollectionChanged += OnCollectionChanged;
    }

    public void ToggleBreakpoint(string filePath, int line, int? column = null)
    {
        BreakpointEntryViewModel? existing = Items.FirstOrDefault(item =>
            string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
            && item.Line == line);

        if (existing is not null)
        {
            Remove(existing);
            return;
        }

        BreakpointEntryViewModel breakpoint = new(filePath, line, column);
        Items.Add(breakpoint);
        BreakpointsChanged?.Invoke();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<SourceBreakpoint>> BuildBreakpointMap()
    {
        Dictionary<string, List<SourceBreakpoint>> results = new(StringComparer.OrdinalIgnoreCase);
        foreach (BreakpointEntryViewModel entry in Items.Where(item => item.IsEnabled))
        {
            if (!results.TryGetValue(entry.FilePath, out List<SourceBreakpoint>? list))
            {
                list = new List<SourceBreakpoint>();
                results[entry.FilePath] = list;
            }

            list.Add(new SourceBreakpoint
            {
                Line = entry.Line,
                Column = entry.Column,
                Condition = entry.Condition,
                HitCondition = entry.HitCondition,
                LogMessage = entry.LogMessage
            });
        }

        return results.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<SourceBreakpoint>)pair.Value);
    }

    public void ApplyResults(string filePath, IReadOnlyList<BreakpointInfo> results)
    {
        foreach (BreakpointEntryViewModel entry in Items)
        {
            if (!string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            BreakpointInfo? match = results.FirstOrDefault(result => result.Line == entry.Line);
            if (match is null)
            {
                entry.IsVerified = false;
                entry.Message = null;
                continue;
            }

            entry.IsVerified = match.IsVerified;
            entry.Message = match.Message;
        }
    }

    private void Clear()
    {
        Items.Clear();
        BreakpointsChanged?.Invoke();
    }

    private void Remove(BreakpointEntryViewModel entry)
    {
        Items.Remove(entry);
        BreakpointsChanged?.Invoke();
    }

    private void RemoveSelected()
    {
        if (SelectedBreakpoint is null)
        {
            return;
        }

        Remove(SelectedBreakpoint);
    }

    private void SetSelectedEnabled(bool enabled)
    {
        if (SelectedBreakpoint is null)
        {
            return;
        }

        SelectedBreakpoint.IsEnabled = enabled;
    }

    private void UpdateSelectedCondition(string? condition)
    {
        if (SelectedBreakpoint is null)
        {
            return;
        }

        SelectedBreakpoint.Condition = condition;
    }

    private void UpdateSelectedHitCondition(string? condition)
    {
        if (SelectedBreakpoint is null)
        {
            return;
        }

        SelectedBreakpoint.HitCondition = condition;
    }

    private void UpdateSelectedLogMessage(string? message)
    {
        if (SelectedBreakpoint is null)
        {
            return;
        }

        SelectedBreakpoint.LogMessage = message;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (BreakpointEntryViewModel entry in e.OldItems)
            {
                Unsubscribe(entry);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (BreakpointEntryViewModel entry in e.NewItems)
            {
                Subscribe(entry);
            }
        }
    }

    private void Subscribe(BreakpointEntryViewModel entry)
    {
        IDisposable subscription = entry.WhenAnyValue(
                x => x.Line,
                x => x.Column,
                x => x.Condition,
                x => x.HitCondition,
                x => x.LogMessage,
                x => x.IsEnabled)
            .Skip(1)
            .Subscribe(_ => BreakpointsChanged?.Invoke());
        _subscriptions[entry] = subscription;
    }

    private void Unsubscribe(BreakpointEntryViewModel entry)
    {
        if (_subscriptions.Remove(entry, out IDisposable? subscription))
        {
            subscription.Dispose();
        }
    }
}

public sealed class StackFrameViewModel : ReactiveObject
{
    public StackFrameViewModel(StackFrameInfo frame)
    {
        FrameId = frame.Id;
        Name = frame.Name;
        FilePath = frame.FilePath;
        Line = frame.Line;
        Column = frame.Column;
        ModuleName = frame.ModuleName;
    }

    public int FrameId { get; }
    public string Name { get; }
    public string? FilePath { get; }
    public int? Line { get; }
    public int? Column { get; }
    public string? ModuleName { get; }
    public string DisplayText => FilePath is null || Line is null
        ? Name
        : $"{Name} ({System.IO.Path.GetFileName(FilePath)}:{Line})";
}

public sealed class CallStackViewModel : ReactiveObject
{
    public ObservableCollection<StackFrameViewModel> Frames { get; } = new();

    [Reactive]
    public StackFrameViewModel? SelectedFrame { get; set; }

    public void ReplaceFrames(IReadOnlyList<StackFrameInfo> frames)
    {
        Frames.Clear();
        foreach (StackFrameInfo frame in frames)
        {
            Frames.Add(new StackFrameViewModel(frame));
        }

        SelectedFrame = Frames.FirstOrDefault();
    }
}

public sealed class VariableViewModel : ReactiveObject
{
    public VariableViewModel(string scopeName, VariableInfo variable)
    {
        ScopeName = scopeName;
        Name = variable.Name;
        Value = variable.Value;
        TypeName = variable.TypeName;
        VariablesReference = variable.VariablesReference;
    }

    public string ScopeName { get; }
    public string Name { get; }
    public string Value { get; }
    public string TypeName { get; }
    public int VariablesReference { get; }
}

public sealed class LocalsViewModel : ReactiveObject
{
    public ObservableCollection<VariableViewModel> Items { get; } = new();

    public void ReplaceVariables(IReadOnlyList<(string Scope, IReadOnlyList<VariableInfo> Variables)> data)
    {
        Items.Clear();
        foreach ((string scope, IReadOnlyList<VariableInfo> variables) in data)
        {
            foreach (VariableInfo variable in variables)
            {
                Items.Add(new VariableViewModel(scope, variable));
            }
        }
    }
}

public sealed class WatchExpressionViewModel : ReactiveObject
{
    public WatchExpressionViewModel(string expression)
    {
        Expression = expression;
    }

    public string Expression { get; }

    [Reactive]
    public string? Result { get; set; }

    [Reactive]
    public string? TypeName { get; set; }
}

public sealed class WatchesViewModel : ReactiveObject
{
    public ObservableCollection<WatchExpressionViewModel> Items { get; } = new();

    [Reactive]
    public string NewExpression { get; set; } = string.Empty;

    [Reactive]
    public WatchExpressionViewModel? SelectedWatch { get; set; }

    public ReactiveCommand<Unit, Unit> AddWatchCommand { get; }
    public ReactiveCommand<WatchExpressionViewModel, Unit> RemoveWatchCommand { get; }

    public WatchesViewModel()
    {
        AddWatchCommand = ReactiveCommand.Create(AddWatch);
        RemoveWatchCommand = ReactiveCommand.Create<WatchExpressionViewModel>(RemoveWatch);
    }

    public void AddWatch(string expression)
    {
        string trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        Items.Add(new WatchExpressionViewModel(trimmed));
    }

    private void AddWatch()
    {
        if (string.IsNullOrWhiteSpace(NewExpression))
        {
            return;
        }

        AddWatch(NewExpression);
        NewExpression = string.Empty;
    }

    private void RemoveWatch(WatchExpressionViewModel watch)
    {
        Items.Remove(watch);
    }
}

public sealed class DebuggerViewModel : ReactiveObject, IDisposable
{
    private readonly IDebuggerService _debuggerService;
    private CompositeDisposable _sessionDisposables = new();
    private IDebugSession? _session;
    private int? _activeThreadId;

    public DebuggerViewModel(IDebuggerService debuggerService)
    {
        _debuggerService = debuggerService;
        Breakpoints = new BreakpointsViewModel();
        CallStack = new CallStackViewModel();
        Locals = new LocalsViewModel();
        Watches = new WatchesViewModel();

        Breakpoints.BreakpointsChanged += OnBreakpointsChanged;
    }

    public BreakpointsViewModel Breakpoints { get; }
    public CallStackViewModel CallStack { get; }
    public LocalsViewModel Locals { get; }
    public WatchesViewModel Watches { get; }

    [Reactive]
    public DebugSessionState State { get; private set; } = DebugSessionState.Created;

    public event Action<DebugOutputEvent>? DebugOutputReceived;
    public event Action<DebugStoppedEvent>? DebugStopped;
    public event Action<DebugContinuedEvent>? DebugContinued;

    public async Task StartAsync(DebugLaunchOptions options, CancellationToken ct = default)
    {
        await StopAsync(ct).ConfigureAwait(false);
        _session = await _debuggerService.LaunchAsync(options, ct).ConfigureAwait(false);
        WireSession(_session);
        await UpdateAllBreakpointsAsync(ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            await _session.DisconnectAsync(true, ct).ConfigureAwait(false);
        }
        catch
        {
        }

        await _session.DisposeAsync().ConfigureAwait(false);
        _session = null;
        State = DebugSessionState.Terminated;
        _sessionDisposables.Dispose();
        _sessionDisposables = new CompositeDisposable();
    }

    public Task ContinueAsync(CancellationToken ct = default)
    {
        return _session is null ? Task.CompletedTask : _session.ContinueAsync(_activeThreadId, ct);
    }

    public Task StepOverAsync(CancellationToken ct = default)
    {
        return _session is null ? Task.CompletedTask : _session.StepOverAsync(_activeThreadId, ct);
    }

    public Task StepInAsync(CancellationToken ct = default)
    {
        return _session is null ? Task.CompletedTask : _session.StepInAsync(_activeThreadId, ct);
    }

    public Task StepOutAsync(CancellationToken ct = default)
    {
        return _session is null ? Task.CompletedTask : _session.StepOutAsync(_activeThreadId, ct);
    }

    public Task PauseAsync(CancellationToken ct = default)
    {
        return _session is null ? Task.CompletedTask : _session.PauseAsync(_activeThreadId, ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (_session is null)
        {
            return;
        }

        IReadOnlyList<ThreadInfo> threads = await _session.GetThreadsAsync(ct).ConfigureAwait(false);
        _activeThreadId = threads.FirstOrDefault()?.Id;
        if (_activeThreadId is null)
        {
            return;
        }

        await RefreshStackAndLocalsAsync(_activeThreadId.Value, ct).ConfigureAwait(false);
    }

    private void WireSession(IDebugSession session)
    {
        IDisposable stateSubscription = Observable.FromEvent<Action<DebugSessionState>, DebugSessionState>(
                handler => state => handler(state),
                h => session.StateChanged += h,
                h => session.StateChanged -= h)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(state => State = state);
        _sessionDisposables.Add(stateSubscription);

        IDisposable eventSubscription = Observable.FromEvent<Action<DebugEvent>, DebugEvent>(
                handler => ev => handler(ev),
                h => session.EventReceived += h,
                h => session.EventReceived -= h)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(HandleDebugEvent);
        _sessionDisposables.Add(eventSubscription);
    }

    private void HandleDebugEvent(DebugEvent ev)
    {
        switch (ev)
        {
            case DebugOutputEvent output:
                DebugOutputReceived?.Invoke(output);
                break;
            case DebugStoppedEvent stopped:
                DebugStopped?.Invoke(stopped);
                _ = RefreshOnStoppedAsync(stopped);
                break;
            case DebugContinuedEvent continued:
                DebugContinued?.Invoke(continued);
                break;
        }
    }

    private async Task RefreshOnStoppedAsync(DebugStoppedEvent stopped)
    {
        if (_session is null)
        {
            return;
        }

        int? threadId = stopped.ThreadId;
        if (threadId is null)
        {
            IReadOnlyList<ThreadInfo> threads = await _session.GetThreadsAsync().ConfigureAwait(false);
            threadId = threads.FirstOrDefault()?.Id;
        }

        if (threadId is null)
        {
            return;
        }

        _activeThreadId = threadId;
        await RefreshStackAndLocalsAsync(threadId.Value).ConfigureAwait(false);
        await RefreshWatchesAsync(threadId.Value).ConfigureAwait(false);
    }

    private async Task RefreshStackAndLocalsAsync(int threadId, CancellationToken ct = default)
    {
        if (_session is null)
        {
            return;
        }

        StackTraceInfo stack = await _session.GetStackTraceAsync(threadId, 0, 50, ct).ConfigureAwait(false);
        CallStack.ReplaceFrames(stack.Frames);
        StackFrameViewModel? frame = CallStack.SelectedFrame;
        if (frame is null)
        {
            return;
        }

        IReadOnlyList<ScopeInfo> scopes = await _session.GetScopesAsync(frame.FrameId, ct).ConfigureAwait(false);
        List<(string Scope, IReadOnlyList<VariableInfo> Variables)> locals = new();
        foreach (ScopeInfo scope in scopes)
        {
            IReadOnlyList<VariableInfo> variables = await _session
                .GetVariablesAsync(scope.VariablesReference, ct)
                .ConfigureAwait(false);
            locals.Add((scope.Name, variables));
        }

        Locals.ReplaceVariables(locals);
    }

    private async Task RefreshWatchesAsync(int threadId, CancellationToken ct = default)
    {
        if (_session is null)
        {
            return;
        }

        int? frameId = CallStack.SelectedFrame?.FrameId;
        foreach (WatchExpressionViewModel watch in Watches.Items)
        {
            EvaluateResult result = await _session
                .EvaluateAsync(new EvaluateRequest { Expression = watch.Expression, FrameId = frameId }, ct)
                .ConfigureAwait(false);
            watch.Result = result.Result;
            watch.TypeName = result.TypeName;
        }
    }

    private void OnBreakpointsChanged()
    {
        _ = UpdateAllBreakpointsAsync();
    }

    private async Task UpdateAllBreakpointsAsync(CancellationToken ct = default)
    {
        if (_session is null)
        {
            return;
        }

        IReadOnlyDictionary<string, IReadOnlyList<SourceBreakpoint>> map = Breakpoints.BuildBreakpointMap();
        foreach ((string filePath, IReadOnlyList<SourceBreakpoint> breakpoints) in map)
        {
            IReadOnlyList<BreakpointInfo> results = await _session
                .SetBreakpointsAsync(filePath, breakpoints, ct)
                .ConfigureAwait(false);
            Breakpoints.ApplyResults(filePath, results);
        }
    }

    public void Dispose()
    {
        Breakpoints.BreakpointsChanged -= OnBreakpointsChanged;
        _sessionDisposables.Dispose();
    }
}
