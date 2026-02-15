using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Dock.Model.Core.Events;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Terminal;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Adapts terminal and task operations for extension access.</summary>
public sealed class TerminalBridgeAdapter : ITerminalBridge, IDisposable
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, TerminalSubscription> _subscriptions = new();
    private Guid? _lastActiveTerminalId;
    private bool _disposed;

    public event EventHandler<TerminalChangedEventArgs>? TerminalCreated;
    public event EventHandler<TerminalChangedEventArgs>? TerminalClosed;
    public event EventHandler<ActiveTerminalChangedEventArgs>? ActiveTerminalChanged;
    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;
    public event EventHandler<TerminalExitEventArgs>? TerminalExited;
    public event EventHandler<TerminalDimensionsChangedEventArgs>? TerminalDimensionsChanged;

    public TerminalBridgeAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _lastActiveTerminalId = _mainViewModel.GetActiveTerminalId();

        foreach (TerminalViewModel terminal in _mainViewModel.Terminals)
        {
            AttachTerminal(terminal);
        }

        _mainViewModel.Terminals.CollectionChanged += OnTerminalsChanged;
        _mainViewModel.DockFactory.ActiveDockableChanged += OnActiveDockableChanged;
    }

    public async Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct)
    {
        return await InvokeOnUiThreadAsync(
            () => CreateTerminal(request),
            ct);
    }

    public async Task SendTextAsync(Guid terminalId, string text, CancellationToken ct)
    {
        await InvokeOnUiThreadAsync(
            () => SendTextCore(terminalId, text),
            ct);
    }

    public async Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(CancellationToken ct)
    {
        return await InvokeOnUiThreadAsync(
            () => _mainViewModel.Terminals.Select(ToTerminalInfo).ToList(),
            ct);
    }

    public async Task<Guid?> GetActiveTerminalIdAsync(CancellationToken ct)
    {
        return await InvokeOnUiThreadAsync(_mainViewModel.GetActiveTerminalId, ct);
    }

    public async Task<bool> CloseAsync(Guid terminalId, CancellationToken ct)
    {
        return await InvokeOnUiThreadAsync(
            () => _mainViewModel.CloseTerminalSession(terminalId),
            ct);
    }

    public async Task<TaskExecutionResult> RunTaskAsync(TaskExecutionRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            throw new ArgumentException("Task command is required.", nameof(request));
        }

        string taskId = string.IsNullOrWhiteSpace(request.TaskId)
            ? request.Command
            : request.TaskId;

        ProcessStartInfo startInfo = new()
        {
            FileName = request.Command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        if (request.Arguments is not null)
        {
            foreach (string arg in request.Arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Task process failed to start.");
        }

        List<(Regex Regex, TaskProblemMatcher Matcher)> matchers = CompileMatchers(request.ProblemMatchers);
        List<string> output = new();
        List<TaskProblemMatch> problems = new();
        object outputGate = new();

        Task readStdOut = ReadLinesAsync(
            process.StandardOutput,
            output,
            problems,
            matchers,
            outputGate,
            ct);
        Task readStdErr = ReadLinesAsync(
            process.StandardError,
            output,
            problems,
            matchers,
            outputGate,
            ct);

        await Task.WhenAll(
            process.WaitForExitAsync(ct),
            readStdOut,
            readStdErr).ConfigureAwait(false);

        return new TaskExecutionResult(taskId, process.ExitCode, output, problems);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mainViewModel.Terminals.CollectionChanged -= OnTerminalsChanged;
        _mainViewModel.DockFactory.ActiveDockableChanged -= OnActiveDockableChanged;

        lock (_gate)
        {
            foreach (TerminalSubscription subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }

    private async Task<T> InvokeOnUiThreadAsync<T>(Func<T> callback, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return callback();
        }

        return await Dispatcher.UIThread.InvokeAsync(callback, DispatcherPriority.Background, ct);
    }

    private async Task InvokeOnUiThreadAsync(Action callback, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            callback();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(callback, DispatcherPriority.Background, ct);
    }

    private TerminalInfo CreateTerminal(TerminalCreateRequest request)
    {
        TerminalSessionOptions options = new()
        {
            WorkingDirectory = request.WorkingDirectory,
            Command = request.ShellPath,
            Arguments = request.Arguments ?? Array.Empty<string>()
        };

        TerminalViewModel terminal = _mainViewModel.CreateTerminalSession(options);
        return ToTerminalInfo(terminal);
    }

    private void SendTextCore(Guid terminalId, string text)
    {
        TerminalViewModel? terminal = _mainViewModel.Terminals
            .FirstOrDefault(vm => vm.Id == terminalId);
        if (terminal is null)
        {
            return;
        }

        terminal.SendText(text);
    }

    private void OnTerminalsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (object item in e.NewItems)
            {
                if (item is not TerminalViewModel terminal)
                {
                    continue;
                }

                AttachTerminal(terminal);
                TerminalCreated?.Invoke(this, new TerminalChangedEventArgs(ToTerminalInfo(terminal)));
            }
        }

        if (e.OldItems is not null)
        {
            foreach (object item in e.OldItems)
            {
                if (item is not TerminalViewModel terminal)
                {
                    continue;
                }

                DetachTerminal(terminal);
                TerminalClosed?.Invoke(this, new TerminalChangedEventArgs(ToTerminalInfo(terminal)));
            }
        }
    }

    private void OnActiveDockableChanged(object? sender, ActiveDockableChangedEventArgs e)
    {
        Guid? next = _mainViewModel.GetActiveTerminalId();
        if (next == _lastActiveTerminalId)
        {
            return;
        }

        _lastActiveTerminalId = next;
        ActiveTerminalChanged?.Invoke(this, new ActiveTerminalChangedEventArgs(next));
    }

    private void AttachTerminal(TerminalViewModel terminal)
    {
        lock (_gate)
        {
            if (_subscriptions.ContainsKey(terminal.Id))
            {
                return;
            }

            Action<string> outputHandler = text =>
            {
                TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(terminal.Id, text));
            };
            Action<int?> exitHandler = exitCode =>
            {
                TerminalExited?.Invoke(this, new TerminalExitEventArgs(terminal.Id, exitCode));
            };
            Action<int, int> dimensionsHandler = (columns, rows) =>
            {
                TerminalDimensionsChanged?.Invoke(
                    this,
                    new TerminalDimensionsChangedEventArgs(terminal.Id, columns, rows));
            };

            terminal.OutputReceived += outputHandler;
            terminal.Exited += exitHandler;
            terminal.DimensionsChanged += dimensionsHandler;
            _subscriptions[terminal.Id] = new TerminalSubscription(terminal, outputHandler, exitHandler, dimensionsHandler);
        }
    }

    private void DetachTerminal(TerminalViewModel terminal)
    {
        lock (_gate)
        {
            if (!_subscriptions.Remove(terminal.Id, out TerminalSubscription? subscription))
            {
                return;
            }

            subscription.Dispose();
        }
    }

    private static TerminalInfo ToTerminalInfo(TerminalViewModel terminal)
    {
        return new TerminalInfo(terminal.Id, terminal.Title, terminal.Columns, terminal.Rows);
    }

    private static List<(Regex Regex, TaskProblemMatcher Matcher)> CompileMatchers(IReadOnlyList<TaskProblemMatcher>? matchers)
    {
        List<(Regex Regex, TaskProblemMatcher Matcher)> compiled = new();
        if (matchers is null || matchers.Count == 0)
        {
            return compiled;
        }

        foreach (TaskProblemMatcher matcher in matchers)
        {
            if (string.IsNullOrWhiteSpace(matcher.Pattern))
            {
                continue;
            }

            compiled.Add((new Regex(matcher.Pattern, RegexOptions.Compiled), matcher));
        }

        return compiled;
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        List<string> output,
        List<TaskProblemMatch> problems,
        List<(Regex Regex, TaskProblemMatcher Matcher)> matchers,
        object gate,
        CancellationToken ct)
    {
        while (true)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            lock (gate)
            {
                output.Add(line);

                if (TryMatchProblem(line, matchers, out TaskProblemMatch? problem) && problem is not null)
                {
                    problems.Add(problem);
                }
            }
        }
    }

    internal static bool TryMatchProblem(
        string line,
        IReadOnlyList<(Regex Regex, TaskProblemMatcher Matcher)> matchers,
        out TaskProblemMatch? problem)
    {
        foreach ((Regex regex, TaskProblemMatcher matcher) in matchers)
        {
            Match match = regex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string? filePath = GetGroupValue(match, matcher.FileGroup);
            int? lineNumber = ParseNullableInt(GetGroupValue(match, matcher.LineGroup));
            int? columnNumber = ParseNullableInt(GetGroupValue(match, matcher.ColumnGroup));
            string? message = GetGroupValue(match, matcher.MessageGroup);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = line;
            }

            problem = new TaskProblemMatch(
                matcher.Severity,
                filePath,
                lineNumber,
                columnNumber,
                message);
            return true;
        }

        problem = null;
        return false;
    }

    private static string? GetGroupValue(Match match, int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= match.Groups.Count)
        {
            return null;
        }

        Group group = match.Groups[groupIndex];
        return group.Success ? group.Value : null;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out int parsed) ? parsed : null;
    }

    private sealed class TerminalSubscription : IDisposable
    {
        private readonly TerminalViewModel _terminal;
        private readonly Action<string> _outputHandler;
        private readonly Action<int?> _exitHandler;
        private readonly Action<int, int> _dimensionsHandler;
        private bool _disposed;

        public TerminalSubscription(
            TerminalViewModel terminal,
            Action<string> outputHandler,
            Action<int?> exitHandler,
            Action<int, int> dimensionsHandler)
        {
            _terminal = terminal;
            _outputHandler = outputHandler;
            _exitHandler = exitHandler;
            _dimensionsHandler = dimensionsHandler;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _terminal.OutputReceived -= _outputHandler;
            _terminal.Exited -= _exitHandler;
            _terminal.DimensionsChanged -= _dimensionsHandler;
        }
    }
}
