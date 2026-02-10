using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace XamlVisualEditor.Terminal;

public sealed class TerminalSession : ITerminalSession
{
    private readonly TerminalSessionOptions _options;
    private readonly IPtyProvider _ptyProvider;
    private readonly ILogger<TerminalSession> _logger;
    private readonly Dictionary<string, int> _unhandledCounts = new(StringComparer.Ordinal);
    private readonly TerminalSequenceLogger? _sequenceLogger;
    private IPtyProcess? _process;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    public TerminalEmulator Emulator { get; }
    public event Action? ScreenUpdated;
    public event Action<string>? TitleChanged;

    public TerminalSession(
        TerminalSessionOptions options,
        IPtyProvider ptyProvider,
        ILogger<TerminalSession>? logger = null)
    {
        _options = options;
        _ptyProvider = ptyProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TerminalSession>.Instance;
        if (_options.EnableSequenceLog && !string.IsNullOrWhiteSpace(_options.SequenceLogPath))
        {
            _sequenceLogger = new TerminalSequenceLogger(_options.SequenceLogPath);
        }
        Emulator = new TerminalEmulator(options.Columns, options.Rows);
        Emulator.SetScrollbackLimit(options.ScrollbackLimit);
        Emulator.ScreenUpdated += () => ScreenUpdated?.Invoke();
        Emulator.TitleChanged += title => TitleChanged?.Invoke(title);
        Emulator.ResponseRequested += OnEmulatorResponseRequested;
        Emulator.UnhandledSequence += OnUnhandledSequence;
    }

    public void Start()
    {
        if (_process is not null)
        {
            return;
        }

        _process = _ptyProvider.StartProcess(_options);
        _cts = new CancellationTokenSource();
        _readLoop = Task.Run(() => ReadLoopAsync(_process.Output, _cts.Token));
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            _sequenceLogger?.LogInput(data);
            _process.Input.Write(data);
            _process.Input.Flush();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Terminal write failed: {Message}", ex.Message);
        }
    }

    public void Resize(int columns, int rows, int pixelWidth = 0, int pixelHeight = 0)
    {
        Emulator.Resize(columns, rows);
        _process?.Resize(columns, rows, pixelWidth, pixelHeight);
    }

    public IReadOnlyList<TerminalCellPosition> ResizeWithMapping(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0)
    {
        IReadOnlyList<TerminalCellPosition> mapped = Emulator.ResizeWithMapping(columns, rows, positions);
        _process?.Resize(columns, rows, pixelWidth, pixelHeight);
        return mapped;
    }

    public IReadOnlyList<TerminalCellPosition> ResizeWithMappingGlobal(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0)
    {
        IReadOnlyList<TerminalCellPosition> mapped = Emulator.ResizeWithMappingGlobal(columns, rows, positions);
        _process?.Resize(columns, rows, pixelWidth, pixelHeight);
        return mapped;
    }

    private async Task ReadLoopAsync(Stream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, ct);
                if (read <= 0)
                {
                    break;
                }

                _sequenceLogger?.LogOutput(buffer.AsSpan(0, read));
                Emulator.ProcessInput(buffer.AsSpan(0, read));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Terminal read failed: {Message}", ex.Message);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _process?.Dispose();
        _sequenceLogger?.Dispose();
    }

    private void OnEmulatorResponseRequested(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return;
        }

        Write(System.Text.Encoding.UTF8.GetBytes(response));
    }

    private void OnUnhandledSequence(string sequence)
    {
        if (string.IsNullOrWhiteSpace(sequence))
        {
            return;
        }

        int count = 0;
        if (_unhandledCounts.TryGetValue(sequence, out int existing))
        {
            count = existing + 1;
        }

        _unhandledCounts[sequence] = count;
        if (count <= 3)
        {
            _logger.LogWarning("Terminal unhandled sequence ({Count}): {Sequence}", count + 1, sequence);
        }
    }
}
