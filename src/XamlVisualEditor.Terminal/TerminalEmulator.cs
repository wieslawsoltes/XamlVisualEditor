using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace XamlVisualEditor.Terminal;

public sealed class TerminalEmulator
{
    private readonly object _sync = new();
    private readonly TerminalParser _parser = new();
    private readonly TerminalHyperlinkStore _hyperlinks = new();
    private TerminalBuffer _mainBuffer;
    private TerminalBuffer _altBuffer;
    private readonly TerminalState _state = new();
    private readonly HashSet<int> _tabStops = new();
    private PrivateModeSnapshot _savedPrivateModes;
    private bool _hasSavedPrivateModes;
    private int _cellWidthPx;
    private int _cellHeightPx;
    private int _pixelWidthPx;
    private int _pixelHeightPx;

    public event Action? ScreenUpdated;
    public event Action<string>? TitleChanged;
    public event Action<string>? ResponseRequested;
    public event Action<string>? UnhandledSequence;
    public event Action<string>? ClipboardCopyRequested;

    public TerminalEmulator(int columns, int rows)
    {
        _mainBuffer = new TerminalBuffer(columns, rows, TerminalAttributes.Default);
        _altBuffer = new TerminalBuffer(columns, rows, TerminalAttributes.Default);
        _state.ScrollTop = 0;
        _state.ScrollBottom = rows - 1;
        InitializeTabStops(columns);
    }

    public TerminalState State => _state;

    public TerminalBuffer ActiveBuffer => _state.AltBufferActive ? _altBuffer : _mainBuffer;

    public void SetDisplayMetrics(int cellWidthPx, int cellHeightPx, int pixelWidthPx, int pixelHeightPx)
    {
        _cellWidthPx = Math.Max(0, cellWidthPx);
        _cellHeightPx = Math.Max(0, cellHeightPx);
        _pixelWidthPx = Math.Max(0, pixelWidthPx);
        _pixelHeightPx = Math.Max(0, pixelHeightPx);
    }

    public void SetScrollbackLimit(int limit)
    {
        int clamped = Math.Max(0, limit);
        _mainBuffer.ScrollbackLimit = clamped;
        _altBuffer.ScrollbackLimit = clamped;
    }

    public void Read(Action<TerminalBuffer, TerminalState> reader)
    {
        lock (_sync)
        {
            reader(ActiveBuffer, _state);
        }
    }

    public void ProcessInput(ReadOnlySpan<byte> data)
    {
        lock (_sync)
        {
            _parser.Process(data, this);
        }
        ScreenUpdated?.Invoke();
    }

    public void Resize(int columns, int rows)
    {
        ResizeWithMapping(columns, rows, Array.Empty<TerminalCellPosition>());
    }

    public IReadOnlyList<TerminalCellPosition> ResizeWithMapping(
        int columns,
        int rows,
        IReadOnlyList<TerminalCellPosition> positions)
    {
        TerminalCellPosition[] mapped;
        lock (_sync)
        {
            InitializeTabStops(columns);
            TerminalCellPosition[] combined = new TerminalCellPosition[positions.Count + 1];
            combined[0] = new TerminalCellPosition(_state.CursorRow, _state.CursorColumn);
            for (int i = 0; i < positions.Count; i++)
            {
                combined[i + 1] = positions[i];
            }

            if (_state.AltBufferActive)
            {
                IReadOnlyList<TerminalCellPosition> bufferMapped = _altBuffer.ReflowResizeWithMapping(columns, rows, TerminalAttributes.Default, combined);
                _mainBuffer.ReflowResize(columns, rows, TerminalAttributes.Default, -1, -1);
                mapped = bufferMapped.ToArray();
            }
            else
            {
                IReadOnlyList<TerminalCellPosition> bufferMapped = _mainBuffer.ReflowResizeWithMapping(columns, rows, TerminalAttributes.Default, combined);
                _altBuffer.ReflowResize(columns, rows, TerminalAttributes.Default, -1, -1);
                mapped = bufferMapped.ToArray();
            }

            if (mapped.Length > 0)
            {
                _state.CursorRow = mapped[0].Row;
                _state.CursorColumn = mapped[0].Column;
            }

            _state.ScrollTop = 0;
            _state.ScrollBottom = rows - 1;
            _state.CursorRow = Math.Clamp(_state.CursorRow, 0, rows - 1);
            _state.CursorColumn = Math.Clamp(_state.CursorColumn, 0, columns - 1);
        }

        ScreenUpdated?.Invoke();

        if (mapped.Length <= 1)
        {
            return Array.Empty<TerminalCellPosition>();
        }

        TerminalCellPosition[] selectionMapped = new TerminalCellPosition[mapped.Length - 1];
        Array.Copy(mapped, 1, selectionMapped, 0, selectionMapped.Length);
        return selectionMapped;
    }

    public IReadOnlyList<TerminalCellPosition> ResizeWithMappingGlobal(
        int columns,
        int rows,
        IReadOnlyList<TerminalCellPosition> positions)
    {
        TerminalCellPosition[] mapped;
        lock (_sync)
        {
            InitializeTabStops(columns);
            int scrollbackCount = ActiveBuffer.ScrollbackCount;
            TerminalCellPosition[] combined = new TerminalCellPosition[positions.Count + 1];
            combined[0] = new TerminalCellPosition(scrollbackCount + _state.CursorRow, _state.CursorColumn);
            for (int i = 0; i < positions.Count; i++)
            {
                combined[i + 1] = positions[i];
            }

            if (_state.AltBufferActive)
            {
                IReadOnlyList<TerminalCellPosition> bufferMapped = _altBuffer.ReflowResizeWithMappingGlobal(columns, rows, TerminalAttributes.Default, combined);
                _mainBuffer.ReflowResize(columns, rows, TerminalAttributes.Default, -1, -1);
                mapped = bufferMapped.ToArray();
            }
            else
            {
                IReadOnlyList<TerminalCellPosition> bufferMapped = _mainBuffer.ReflowResizeWithMappingGlobal(columns, rows, TerminalAttributes.Default, combined);
                _altBuffer.ReflowResize(columns, rows, TerminalAttributes.Default, -1, -1);
                mapped = bufferMapped.ToArray();
            }

            if (mapped.Length > 0)
            {
                int newScrollback = ActiveBuffer.ScrollbackCount;
                _state.CursorRow = Math.Clamp(mapped[0].Row - newScrollback, 0, rows - 1);
                _state.CursorColumn = Math.Clamp(mapped[0].Column, 0, columns - 1);
            }

            _state.ScrollTop = 0;
            _state.ScrollBottom = rows - 1;
            _state.CursorRow = Math.Clamp(_state.CursorRow, 0, rows - 1);
            _state.CursorColumn = Math.Clamp(_state.CursorColumn, 0, columns - 1);
        }

        ScreenUpdated?.Invoke();

        if (mapped.Length <= 1)
        {
            return Array.Empty<TerminalCellPosition>();
        }

        TerminalCellPosition[] selectionMapped = new TerminalCellPosition[mapped.Length - 1];
        Array.Copy(mapped, 1, selectionMapped, 0, selectionMapped.Length);
        return selectionMapped;
    }

    public void WriteRune(Rune rune)
    {
        TerminalBuffer buffer = ActiveBuffer;
        rune = ApplyCharset(rune);
        int width = UnicodeWidth.GetWidth(rune);
        if (width == 0)
        {
            return;
        }

        if (_state.AutoWrap && _state.WrapPending)
        {
            _state.WrapPending = false;
            LineFeed();
            CarriageReturn();
        }

        if (!_state.AutoWrap && _state.CursorColumn >= buffer.Columns)
        {
            _state.CursorColumn = buffer.Columns - 1;
        }

        if (_state.AutoWrap && width == 2 && _state.CursorColumn == buffer.Columns - 1)
        {
            buffer.GetLine(_state.CursorRow).IsWrapped = true;
            LineFeed();
            CarriageReturn();
        }

        TerminalLine line = buffer.GetLine(_state.CursorRow);
        TerminalAttributes attrs = _state.Attributes;
        int? hyperlinkId = _state.ActiveHyperlinkId;

        int available = Math.Max(0, buffer.Columns - _state.CursorColumn);
        int widthToWrite = Math.Min(width, available);
        if (widthToWrite <= 0)
        {
            return;
        }

        if (_state.InsertMode)
        {
            buffer.InsertChars(_state.CursorRow, _state.CursorColumn, widthToWrite, attrs);
        }

        line.Cells[_state.CursorColumn] = new TerminalCell(rune, (byte)widthToWrite, attrs, hyperlinkId);
        if (widthToWrite == 2 && _state.CursorColumn + 1 < buffer.Columns)
        {
            line.Cells[_state.CursorColumn + 1] = new TerminalCell(new Rune(' '), 0, attrs, hyperlinkId);
        }

        if (_state.AutoWrap && _state.CursorColumn + widthToWrite >= buffer.Columns)
        {
            _state.CursorColumn = buffer.Columns - 1;
            _state.WrapPending = true;
            line.IsWrapped = true;
        }
        else
        {
            _state.CursorColumn += widthToWrite;
        }
    }

    public void HandleControl(byte code)
    {
        switch (code)
        {
            case 0x07: // BEL
                break;
            case 0x08: // BS
                _state.CursorColumn = Math.Max(0, _state.CursorColumn - 1);
                break;
            case 0x0E: // SO
                _state.UseG1Charset = true;
                break;
            case 0x0F: // SI
                _state.UseG1Charset = false;
                break;
            case 0x09: // TAB
                _state.CursorColumn = NextTabStop(_state.CursorColumn, ActiveBuffer.Columns);
                break;
            case 0x0A: // LF
                LineFeed();
                break;
            case 0x0B: // VT
                LineFeed();
                break;
            case 0x0C: // FF
                LineFeed();
                break;
            case 0x0D: // CR
                CarriageReturn();
                break;
        }
    }

    public void HandleEscape(char code)
    {
        switch (code)
        {
            case '7':
                _state.SavedCursorRow = _state.CursorRow;
                _state.SavedCursorColumn = _state.CursorColumn;
                break;
            case '8':
                _state.CursorRow = _state.SavedCursorRow;
                _state.CursorColumn = _state.SavedCursorColumn;
                break;
            case 'M':
                ReverseIndex();
                break;
            case 'D':
                LineFeed();
                break;
            case 'E':
                LineFeed();
                CarriageReturn();
                break;
            case 'Z':
                ResponseRequested?.Invoke("\x1B[?1;2c");
                break;
            case 'H':
                _tabStops.Add(_state.CursorColumn);
                break;
            case 'c':
                Reset();
                break;
            case '=':
                _state.ApplicationKeypad = true;
                break;
            case '>':
                _state.ApplicationKeypad = false;
                break;
            default:
                UnhandledSequence?.Invoke($"ESC {code}");
                break;
        }
    }

    private void ReverseIndex()
    {
        if (_state.CursorRow == _state.ScrollTop)
        {
            ActiveBuffer.ScrollDown(_state.ScrollTop, _state.ScrollBottom, _state.Attributes);
        }
        else
        {
            _state.CursorRow = Math.Max(_state.CursorRow - 1, 0);
        }

        _state.WrapPending = false;
    }

    public void HandleCharsetSelect(bool g1, char designator)
    {
        TerminalCharset charset = designator switch
        {
            '0' => TerminalCharset.DecSpecialGraphics,
            'A' => TerminalCharset.Uk,
            '4' => TerminalCharset.Dutch,
            'C' => TerminalCharset.Finnish,
            '5' => TerminalCharset.Finnish,
            'R' => TerminalCharset.French,
            'Q' => TerminalCharset.FrenchCanadian,
            'K' => TerminalCharset.German,
            'Y' => TerminalCharset.Italian,
            'E' => TerminalCharset.NorwegianDanish,
            '6' => TerminalCharset.NorwegianDanish,
            'Z' => TerminalCharset.Spanish,
            'H' => TerminalCharset.Swedish,
            '7' => TerminalCharset.Swedish,
            '=' => TerminalCharset.Swiss,
            '<' => TerminalCharset.DecSupplemental,
            '|' => TerminalCharset.DecSupplemental,
            'B' => TerminalCharset.Ascii,
            _ => TerminalCharset.Ascii
        };

        if (g1)
        {
            _state.CharsetG1 = charset;
        }
        else
        {
            _state.CharsetG0 = charset;
        }
    }

    public void HandleEscapePercent(char code)
    {
        switch (code)
        {
            case 'G':
                _state.Utf8Mode = true;
                break;
            case '@':
                _state.Utf8Mode = false;
                break;
            default:
                UnhandledSequence?.Invoke($"ESC %{code}");
                break;
        }
    }

    public void HandleEscapeHash(char code)
    {
        if (code != '8')
        {
            UnhandledSequence?.Invoke($"ESC #{code}");
            return;
        }

        TerminalBuffer buffer = ActiveBuffer;
        TerminalAttributes attrs = _state.Attributes;
        for (int row = 0; row < buffer.Rows; row++)
        {
            TerminalLine line = buffer.GetLine(row);
            for (int col = 0; col < buffer.Columns; col++)
            {
                line.Cells[col] = new TerminalCell(new Rune('E'), 1, attrs);
            }
            line.IsWrapped = false;
        }
    }

    public void HandleCsi(char code, IReadOnlyList<int> parameters, char privatePrefix)
    {
        int p1 = parameters.Count > 0 ? parameters[0] : 0;
        switch (code)
        {
            case 'c':
                if (privatePrefix == '>')
                {
                    ResponseRequested?.Invoke("\x1B[>0;115;0c");
                }
                else
                {
                    ResponseRequested?.Invoke("\x1B[?62;1;2;6;9;15;22c");
                }
                break;
            case 'A':
                MoveCursor(-Math.Max(1, p1), 0);
                break;
            case 'B':
                MoveCursor(Math.Max(1, p1), 0);
                break;
            case 'C':
                MoveCursor(0, Math.Max(1, p1));
                break;
            case 'D':
                MoveCursor(0, -Math.Max(1, p1));
                break;
            case 'H':
            case 'f':
                SetCursorPosition(parameters);
                break;
            case 'G':
                SetCursorColumn(parameters);
                break;
            case 'd':
                SetCursorRow(parameters);
                break;
            case 'J':
                EraseDisplay(p1);
                break;
            case 'K':
                EraseLine(p1);
                break;
            case 'g':
                ClearTabStops(p1);
                break;
            case '@':
                InsertChars(Math.Max(1, p1));
                break;
            case 'P':
                DeleteChars(Math.Max(1, p1));
                break;
            case 'X':
                EraseChars(Math.Max(1, p1));
                break;
            case 'L':
                InsertLines(Math.Max(1, p1));
                break;
            case 'M':
                DeleteLines(Math.Max(1, p1));
                break;
            case 'S':
                ScrollUp(Math.Max(1, p1));
                break;
            case 'T':
                ScrollDown(Math.Max(1, p1));
                break;
            case 'm':
                ApplySgr(parameters);
                break;
            case 's':
                if (privatePrefix == '?')
                {
                    SavePrivateModes();
                }
                else
                {
                    _state.SavedCursorRow = _state.CursorRow;
                    _state.SavedCursorColumn = _state.CursorColumn;
                }
                break;
            case 'u':
                _state.CursorRow = _state.SavedCursorRow;
                _state.CursorColumn = _state.SavedCursorColumn;
                break;
            case 'r':
                if (privatePrefix == '?')
                {
                    RestorePrivateModes();
                }
                else
                {
                    SetScrollRegion(parameters);
                }
                break;
            case 'n':
                HandleDeviceStatusReport(p1);
                break;
            case 'h':
                if (privatePrefix == '?')
                {
                    SetPrivateModes(parameters, enabled: true);
                }
                else
                {
                    SetModes(parameters, enabled: true);
                }
                break;
            case 'l':
                if (privatePrefix == '?')
                {
                    SetPrivateModes(parameters, enabled: false);
                }
                else
                {
                    SetModes(parameters, enabled: false);
                }
                break;
            case 'q':
                if (privatePrefix == '>')
                {
                    break;
                }
                goto default;
            default:
                UnhandledSequence?.Invoke(BuildCsiSequence(code, parameters, privatePrefix));
                break;
        }
    }

    public void HandleOsc(string payload)
    {
        int separator = payload.IndexOf(';');
        if (separator <= 0)
        {
            return;
        }

        string command = payload[..separator];
        string data = payload[(separator + 1)..];

        if (command is "0" or "2")
        {
            _state.WindowTitle = data;
            TitleChanged?.Invoke(data);
            return;
        }

        if (command == "7")
        {
            return;
        }

        if (command == "8")
        {
            int secondSep = data.IndexOf(';');
            if (secondSep < 0)
            {
                return;
            }

            string url = data[(secondSep + 1)..];
            if (string.IsNullOrEmpty(url))
            {
                _state.ActiveHyperlinkId = null;
                return;
            }

            _state.ActiveHyperlinkId = _hyperlinks.Add(url);
            return;
        }

        if (command == "10" && data == "?")
        {
            TerminalRgb fg = TerminalTheme.DefaultDark.Foreground;
            ResponseRequested?.Invoke($"\x1B]10;rgb:{fg.R:X2}{fg.R:X2}/{fg.G:X2}{fg.G:X2}/{fg.B:X2}{fg.B:X2}\x1B\\");
            return;
        }

        if (command == "11" && data == "?")
        {
            TerminalRgb bg = TerminalTheme.DefaultDark.Background;
            ResponseRequested?.Invoke($"\x1B]11;rgb:{bg.R:X2}{bg.R:X2}/{bg.G:X2}{bg.G:X2}/{bg.B:X2}{bg.B:X2}\x1B\\");
            return;
        }

        if (command == "9" && data.StartsWith("4;0;", StringComparison.Ordinal))
        {
            return;
        }

        if (command == "10" && data == "?")
        {
            TerminalRgb fg = TerminalTheme.DefaultDark.Foreground;
            ResponseRequested?.Invoke($"\x1B]10;rgb:{fg.R:X2}{fg.R:X2}/{fg.G:X2}{fg.G:X2}/{fg.B:X2}{fg.B:X2}\x1B\\");
            return;
        }

        if (command == "52")
        {
            HandleOsc52(data);
            return;
        }

        UnhandledSequence?.Invoke($"OSC {command};{data}");
    }

    private void HandleOsc52(string data)
    {
        int sep = data.IndexOf(';');
        if (sep < 0)
        {
            return;
        }

        string payload = data[(sep + 1)..];
        if (string.IsNullOrWhiteSpace(payload) || payload == "?")
        {
            return;
        }

        try
        {
            byte[] decoded = Convert.FromBase64String(payload);
            string text = Encoding.UTF8.GetString(decoded);
            if (!string.IsNullOrEmpty(text))
            {
                ClipboardCopyRequested?.Invoke(text);
            }
        }
        catch (FormatException)
        {
        }
    }

    private static string BuildCsiSequence(char code, IReadOnlyList<int> parameters, char privatePrefix)
    {
        StringBuilder builder = new();
        builder.Append("CSI ");
        if (privatePrefix != '\0')
        {
            builder.Append(privatePrefix);
        }

        if (parameters.Count > 0)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(';');
                }

                builder.Append(parameters[i]);
            }
        }

        builder.Append(' ');
        builder.Append(code);
        return builder.ToString();
    }

    private void HandleWindowOps(IReadOnlyList<int> parameters)
    {
        int op = parameters.Count > 0 ? parameters[0] : 0;
        switch (op)
        {
            case 14:
                ResponseRequested?.Invoke($"\x1B[4;{_pixelHeightPx};{_pixelWidthPx}t");
                break;
            case 16:
                ResponseRequested?.Invoke($"\x1B[6;{_cellHeightPx};{_cellWidthPx}t");
                break;
            case 18:
                ResponseRequested?.Invoke($"\x1B[8;{ActiveBuffer.Rows};{ActiveBuffer.Columns}t");
                break;
        }
    }

    private void InitializeTabStops(int columns)
    {
        _tabStops.Clear();
        for (int i = 8; i < columns; i += 8)
        {
            _tabStops.Add(i);
        }
    }

    private int NextTabStop(int column, int columns)
    {
        if (_tabStops.Count == 0)
        {
            return Math.Min(columns - 1, ((column / 8) + 1) * 8);
        }

        for (int i = column + 1; i < columns; i++)
        {
            if (_tabStops.Contains(i))
            {
                return i;
            }
        }

        return columns - 1;
    }

    private void ClearTabStops(int mode)
    {
        switch (mode)
        {
            case 0:
                _tabStops.Remove(_state.CursorColumn);
                break;
            case 3:
                _tabStops.Clear();
                break;
        }
    }

    private void HandleOscPaletteQuery(string data)
    {
        string[] parts = data.Split(';');
        if (parts.Length < 2)
        {
            return;
        }

        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (!int.TryParse(parts[i], out int index))
            {
                continue;
            }

            if (!string.Equals(parts[i + 1], "?", StringComparison.Ordinal))
            {
                continue;
            }

            index = Math.Clamp(index, 0, 255);
            TerminalRgb color = TerminalPalette.ResolveIndex((byte)index);
            string response = $"\x1B]4;{index};rgb:{color.R:X2}{color.R:X2}/{color.G:X2}{color.G:X2}/{color.B:X2}{color.B:X2}\x1B\\";
            ResponseRequested?.Invoke(response);
        }
    }

    public string? GetHyperlink(int? id)
    {
        if (id is null)
        {
            return null;
        }

        return _hyperlinks.TryGet(id.Value);
    }

    public void Reset()
    {
        _state.Attributes = TerminalAttributes.Default;
        _state.CursorRow = 0;
        _state.CursorColumn = 0;
        _state.ScrollTop = 0;
        _state.ScrollBottom = ActiveBuffer.Rows - 1;
        _state.AltBufferActive = false;
        _state.MouseMode = TerminalMouseMode.None;
        _state.MouseSgr = false;
        _state.MouseProtocol = TerminalMouseProtocol.Vt200;
        _state.MouseX10 = false;
        _state.BracketedPaste = false;
        _state.ApplicationKeypad = false;
        _state.ApplicationCursorKeys = false;
        _state.Utf8Mode = true;
        _state.CursorBlink = true;
        _state.CursorShape = TerminalCursorShape.Block;
        _state.CharsetG0 = TerminalCharset.Ascii;
        _state.CharsetG1 = TerminalCharset.Ascii;
        _state.UseG1Charset = false;
        _state.WrapPending = false;
        _hasSavedPrivateModes = false;
        _savedPrivateModes = default;
        _mainBuffer.Clear(TerminalAttributes.Default);
        _altBuffer.Clear(TerminalAttributes.Default);
    }

    private static readonly int[] NrcUk =
    {
        0x00A3, 0x0040, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F, 0x0060, 0x007B, 0x007C, 0x007D, 0x007E
    };

    private static readonly int[] NrcDutch =
    {
        0x00A3, 0x00BE, 0x0133, 0x00BD, 0x007C, 0x005E, 0x005F, 0x0060, 0x00A8, 0x0192, 0x00BC, 0x00B4
    };

    private static readonly int[] NrcFinnish =
    {
        0x0023, 0x0040, 0x00C4, 0x00D6, 0x00C5, 0x00DC, 0x005F, 0x00E9, 0x00E4, 0x00F6, 0x00E5, 0x00FC
    };

    private static readonly int[] NrcFrench =
    {
        0x00A3, 0x00E0, 0x00B0, 0x00E7, 0x00A7, 0x005E, 0x005F, 0x0060, 0x00E9, 0x00F9, 0x00E8, 0x00A8
    };

    private static readonly int[] NrcFrenchCanadian =
    {
        0x0023, 0x00E0, 0x00E2, 0x00E7, 0x00EA, 0x00EE, 0x005F, 0x00F4, 0x00E9, 0x00F9, 0x00E8, 0x00FB
    };

    private static readonly int[] NrcGerman =
    {
        0x0023, 0x00A7, 0x00C4, 0x00D6, 0x00DC, 0x005E, 0x005F, 0x0060, 0x00E4, 0x00F6, 0x00FC, 0x00DF
    };

    private static readonly int[] NrcItalian =
    {
        0x00A3, 0x00A7, 0x00B0, 0x00E7, 0x00E9, 0x005E, 0x005F, 0x00F9, 0x00E0, 0x00F2, 0x00E8, 0x00EC
    };

    private static readonly int[] NrcNorwegianDanish =
    {
        0x0023, 0x00C4, 0x00C6, 0x00D8, 0x00C5, 0x00DC, 0x005F, 0x00E4, 0x00E6, 0x00F8, 0x00E5, 0x00FC
    };

    private static readonly int[] NrcSpanish =
    {
        0x00A3, 0x00A7, 0x00A1, 0x00D1, 0x00BF, 0x005E, 0x005F, 0x0060, 0x00B0, 0x00F1, 0x00E7, 0x007E
    };

    private static readonly int[] NrcSwedish =
    {
        0x0023, 0x00C9, 0x00C4, 0x00D6, 0x00C5, 0x00DC, 0x005F, 0x00E9, 0x00E4, 0x00F6, 0x00E5, 0x00FC
    };

    private static readonly int[] NrcSwiss =
    {
        0x00F9, 0x00E0, 0x00E9, 0x00E7, 0x00EA, 0x00EE, 0x00E8, 0x00F4, 0x00E4, 0x00F6, 0x00FC, 0x00FB
    };

    private Rune ApplyCharset(Rune rune)
    {
        TerminalCharset active = _state.UseG1Charset ? _state.CharsetG1 : _state.CharsetG0;
        if (active == TerminalCharset.DecSupplemental)
        {
            if (rune.Value >= 0x21 && rune.Value <= 0x7E)
            {
                return new Rune(rune.Value + 0x80);
            }
            return rune;
        }

        if (TryApplyNrcCharset(active, rune, out Rune mapped))
        {
            return mapped;
        }

        if (active != TerminalCharset.DecSpecialGraphics || rune.Value < 0x60 || rune.Value > 0x7E)
        {
            return rune;
        }

        return rune.Value switch
        {
            0x60 => new Rune('◆'),
            0x61 => new Rune('▒'),
            0x62 => new Rune('␉'),
            0x63 => new Rune('␌'),
            0x64 => new Rune('␍'),
            0x65 => new Rune('␊'),
            0x66 => new Rune('°'),
            0x67 => new Rune('±'),
            0x68 => new Rune('␤'),
            0x69 => new Rune('␋'),
            0x6A => new Rune('┘'),
            0x6B => new Rune('┐'),
            0x6C => new Rune('┌'),
            0x6D => new Rune('└'),
            0x6E => new Rune('┼'),
            0x6F => new Rune('⎺'),
            0x70 => new Rune('⎻'),
            0x71 => new Rune('─'),
            0x72 => new Rune('⎼'),
            0x73 => new Rune('⎽'),
            0x74 => new Rune('├'),
            0x75 => new Rune('┤'),
            0x76 => new Rune('┴'),
            0x77 => new Rune('┬'),
            0x78 => new Rune('│'),
            0x79 => new Rune('≤'),
            0x7A => new Rune('≥'),
            0x7B => new Rune('π'),
            0x7C => new Rune('≠'),
            0x7D => new Rune('£'),
            0x7E => new Rune('·'),
            _ => rune
        };
    }

    private static bool TryApplyNrcCharset(TerminalCharset charset, Rune rune, out Rune mapped)
    {
        mapped = rune;
        int index = rune.Value switch
        {
            0x23 => 0,
            0x40 => 1,
            0x5B => 2,
            0x5C => 3,
            0x5D => 4,
            0x5E => 5,
            0x5F => 6,
            0x60 => 7,
            0x7B => 8,
            0x7C => 9,
            0x7D => 10,
            0x7E => 11,
            _ => -1
        };

        if (index < 0)
        {
            return false;
        }

        int mappedValue = charset switch
        {
            TerminalCharset.Uk => NrcUk[index],
            TerminalCharset.Dutch => NrcDutch[index],
            TerminalCharset.Finnish => NrcFinnish[index],
            TerminalCharset.French => NrcFrench[index],
            TerminalCharset.FrenchCanadian => NrcFrenchCanadian[index],
            TerminalCharset.German => NrcGerman[index],
            TerminalCharset.Italian => NrcItalian[index],
            TerminalCharset.NorwegianDanish => NrcNorwegianDanish[index],
            TerminalCharset.Spanish => NrcSpanish[index],
            TerminalCharset.Swedish => NrcSwedish[index],
            TerminalCharset.Swiss => NrcSwiss[index],
            _ => -1
        };

        if (mappedValue < 0)
        {
            return false;
        }

        mapped = new Rune(mappedValue);
        return true;
    }

    private void MoveCursor(int rowDelta, int colDelta)
    {
        TerminalBuffer buffer = ActiveBuffer;
        int row = _state.CursorRow + rowDelta;
        int col = _state.CursorColumn + colDelta;
        int minRow = _state.OriginMode ? _state.ScrollTop : 0;
        int maxRow = _state.OriginMode ? _state.ScrollBottom : buffer.Rows - 1;
        _state.CursorRow = Math.Clamp(row, minRow, maxRow);
        _state.CursorColumn = Math.Clamp(col, 0, buffer.Columns - 1);
        _state.WrapPending = false;
    }

    private void SetCursorPosition(IReadOnlyList<int> parameters)
    {
        TerminalBuffer buffer = ActiveBuffer;
        int row = parameters.Count > 0 ? Math.Max(1, parameters[0]) : 1;
        int col = parameters.Count > 1 ? Math.Max(1, parameters[1]) : 1;
        int targetRow = row - 1;
        if (_state.OriginMode)
        {
            targetRow += _state.ScrollTop;
        }
        int minRow = _state.OriginMode ? _state.ScrollTop : 0;
        int maxRow = _state.OriginMode ? _state.ScrollBottom : buffer.Rows - 1;
        _state.CursorRow = Math.Clamp(targetRow, minRow, maxRow);
        _state.CursorColumn = Math.Clamp(col - 1, 0, buffer.Columns - 1);
        _state.WrapPending = false;
    }

    private void SetCursorRow(IReadOnlyList<int> parameters)
    {
        TerminalBuffer buffer = ActiveBuffer;
        int row = parameters.Count > 0 ? Math.Max(1, parameters[0]) : 1;
        int targetRow = row - 1;
        if (_state.OriginMode)
        {
            targetRow += _state.ScrollTop;
        }
        int minRow = _state.OriginMode ? _state.ScrollTop : 0;
        int maxRow = _state.OriginMode ? _state.ScrollBottom : buffer.Rows - 1;
        _state.CursorRow = Math.Clamp(targetRow, minRow, maxRow);
        _state.WrapPending = false;
    }

    private void SetCursorColumn(IReadOnlyList<int> parameters)
    {
        TerminalBuffer buffer = ActiveBuffer;
        int col = parameters.Count > 0 ? Math.Max(1, parameters[0]) : 1;
        _state.CursorColumn = Math.Clamp(col - 1, 0, buffer.Columns - 1);
    }

    private void LineFeed()
    {
        if (_state.CursorRow == _state.ScrollBottom)
        {
            ActiveBuffer.ScrollUp(_state.ScrollTop, _state.ScrollBottom, _state.Attributes);
        }
        else
        {
            _state.CursorRow = Math.Min(_state.CursorRow + 1, ActiveBuffer.Rows - 1);
        }
        _state.WrapPending = false;
    }

    private void CarriageReturn()
    {
        _state.CursorColumn = 0;
        _state.WrapPending = false;
    }

    private void InsertChars(int count)
    {
        TerminalBuffer buffer = ActiveBuffer;
        buffer.InsertChars(_state.CursorRow, _state.CursorColumn, count, _state.Attributes);
    }

    private void DeleteChars(int count)
    {
        TerminalBuffer buffer = ActiveBuffer;
        buffer.DeleteChars(_state.CursorRow, _state.CursorColumn, count, _state.Attributes);
    }

    private void EraseChars(int count)
    {
        TerminalBuffer buffer = ActiveBuffer;
        buffer.EraseChars(_state.CursorRow, _state.CursorColumn, count, _state.Attributes);
    }

    private void InsertLines(int count)
    {
        TerminalBuffer buffer = ActiveBuffer;
        buffer.InsertLines(_state.CursorRow, count, _state.ScrollTop, _state.ScrollBottom, _state.Attributes);
    }

    private void DeleteLines(int count)
    {
        TerminalBuffer buffer = ActiveBuffer;
        buffer.DeleteLines(_state.CursorRow, count, _state.ScrollTop, _state.ScrollBottom, _state.Attributes);
    }

    private void ScrollUp(int count)
    {
        TerminalBuffer buffer = ActiveBuffer;
        for (int i = 0; i < count; i++)
        {
            buffer.ScrollUp(_state.ScrollTop, _state.ScrollBottom, _state.Attributes);
        }
    }

    private void ScrollDown(int count)
    {
        TerminalBuffer buffer = ActiveBuffer;
        for (int i = 0; i < count; i++)
        {
            buffer.ScrollDown(_state.ScrollTop, _state.ScrollBottom, _state.Attributes);
        }
    }

    private void HandleDeviceStatusReport(int parameter)
    {
        switch (parameter)
        {
            case 5:
                ResponseRequested?.Invoke("\x1B[0n");
                break;
            case 6:
                int row = _state.CursorRow + 1;
                int col = _state.CursorColumn + 1;
                ResponseRequested?.Invoke($"\x1B[{row};{col}R");
                break;
        }
    }

    private void EraseDisplay(int mode)
    {
        TerminalBuffer buffer = ActiveBuffer;
        switch (mode)
        {
            case 0:
                EraseLine(0);
                for (int row = _state.CursorRow + 1; row < buffer.Rows; row++)
                {
                    buffer.ClearLine(row, _state.Attributes);
                }
                break;
            case 1:
                for (int row = 0; row < _state.CursorRow; row++)
                {
                    buffer.ClearLine(row, _state.Attributes);
                }
                EraseLine(1);
                break;
            case 2:
                buffer.Clear(_state.Attributes);
                break;
            case 3:
                buffer.Clear(_state.Attributes);
                buffer.ClearScrollback();
                break;
        }
    }

    private void EraseLine(int mode)
    {
        TerminalLine line = ActiveBuffer.GetLine(_state.CursorRow);
        switch (mode)
        {
            case 0:
                for (int col = _state.CursorColumn; col < line.Cells.Length; col++)
                {
                    line.Cells[col] = TerminalCell.Empty(_state.Attributes);
                }
                break;
            case 1:
                for (int col = 0; col <= _state.CursorColumn; col++)
                {
                    line.Cells[col] = TerminalCell.Empty(_state.Attributes);
                }
                break;
            case 2:
                line.Clear(_state.Attributes);
                break;
        }
    }

    private void ApplySgr(IReadOnlyList<int> parameters)
    {
        if (parameters.Count == 0)
        {
            _state.Attributes = TerminalAttributes.Default;
            return;
        }

        TerminalAttributes attrs = _state.Attributes;
        int i = 0;
        while (i < parameters.Count)
        {
            int param = parameters[i++];
            switch (param)
            {
                case 0:
                    attrs = TerminalAttributes.Default;
                    break;
                case 1:
                    attrs = attrs.With(bold: true);
                    break;
                case 2:
                    attrs = attrs.With(dim: true);
                    break;
                case 3:
                    attrs = attrs.With(italic: true);
                    break;
                case 4:
                    attrs = attrs.With(underline: true);
                    break;
                case 5:
                    attrs = attrs.With(blink: true);
                    break;
                case 7:
                    attrs = attrs.With(inverse: true);
                    break;
                case 9:
                    attrs = attrs.With(strikethrough: true);
                    break;
                case 22:
                    attrs = attrs.With(bold: false, dim: false);
                    break;
                case 23:
                    attrs = attrs.With(italic: false);
                    break;
                case 24:
                    attrs = attrs.With(underline: false);
                    break;
                case 27:
                    attrs = attrs.With(inverse: false);
                    break;
                case 29:
                    attrs = attrs.With(strikethrough: false);
                    break;
                case int fg when fg >= 30 && fg <= 37:
                    attrs = attrs.With(foreground: TerminalColor.FromIndex((byte)(fg - 30)));
                    break;
                case int fg when fg >= 90 && fg <= 97:
                    attrs = attrs.With(foreground: TerminalColor.FromIndex((byte)(fg - 90 + 8)));
                    break;
                case int bg when bg >= 40 && bg <= 47:
                    attrs = attrs.With(background: TerminalColor.FromIndex((byte)(bg - 40)));
                    break;
                case int bg when bg >= 100 && bg <= 107:
                    attrs = attrs.With(background: TerminalColor.FromIndex((byte)(bg - 100 + 8)));
                    break;
                case 38:
                    if (TryParseColor(parameters, ref i, out TerminalColor fgColor))
                    {
                        attrs = attrs.With(foreground: fgColor);
                    }
                    break;
                case 48:
                    if (TryParseColor(parameters, ref i, out TerminalColor bgColor))
                    {
                        attrs = attrs.With(background: bgColor);
                    }
                    break;
                case 39:
                    attrs = attrs.With(foreground: TerminalColor.Default);
                    break;
                case 49:
                    attrs = attrs.With(background: TerminalColor.Default);
                    break;
            }
        }

        _state.Attributes = attrs;
    }

    private static bool TryParseColor(IReadOnlyList<int> parameters, ref int index, out TerminalColor color)
    {
        color = TerminalColor.Default;
        if (index >= parameters.Count)
        {
            return false;
        }

        int mode = parameters[index++];
        if (mode == 5 && index < parameters.Count)
        {
            int value = parameters[index++];
            color = TerminalColor.FromIndex((byte)Math.Clamp(value, 0, 255));
            return true;
        }

        if (mode == 2 && index + 2 < parameters.Count)
        {
            byte r = (byte)Math.Clamp(parameters[index++], 0, 255);
            byte g = (byte)Math.Clamp(parameters[index++], 0, 255);
            byte b = (byte)Math.Clamp(parameters[index++], 0, 255);
            color = TerminalColor.FromRgb(r, g, b);
            return true;
        }

        return false;
    }

    private void SetScrollRegion(IReadOnlyList<int> parameters)
    {
        int top = parameters.Count > 0 ? Math.Max(1, parameters[0]) : 1;
        int bottom = parameters.Count > 1 ? Math.Max(1, parameters[1]) : ActiveBuffer.Rows;
        top = Math.Clamp(top, 1, ActiveBuffer.Rows) - 1;
        bottom = Math.Clamp(bottom, 1, ActiveBuffer.Rows) - 1;
        if (top >= bottom)
        {
            return;
        }

        _state.ScrollTop = top;
        _state.ScrollBottom = bottom;
        _state.CursorRow = _state.OriginMode ? top : 0;
        _state.CursorColumn = 0;
        _state.WrapPending = false;
    }

    private void SetPrivateModes(IReadOnlyList<int> parameters, bool enabled)
    {
        foreach (int param in parameters)
        {
            switch (param)
            {
                case 1049:
                case 47:
                case 1047:
                    _state.AltBufferActive = enabled;
                    if (enabled)
                    {
                        _altBuffer.Clear(TerminalAttributes.Default);
                        _state.CursorRow = 0;
                        _state.CursorColumn = 0;
                    }
                    break;
                case 1000:
                    _state.MouseMode = enabled ? TerminalMouseMode.Click : TerminalMouseMode.None;
                    break;
                case 1002:
                    _state.MouseMode = enabled ? TerminalMouseMode.Drag : TerminalMouseMode.None;
                    break;
                case 1003:
                    _state.MouseMode = enabled ? TerminalMouseMode.Move : TerminalMouseMode.None;
                    break;
                case 9:
                    _state.MouseX10 = enabled;
                    if (enabled)
                    {
                        _state.MouseProtocol = TerminalMouseProtocol.X10;
                    }
                    else if (_state.MouseProtocol == TerminalMouseProtocol.X10)
                    {
                        _state.MouseProtocol = TerminalMouseProtocol.Vt200;
                    }
                    break;
                case 1006:
                    _state.MouseSgr = enabled;
                    _state.MouseProtocol = enabled ? TerminalMouseProtocol.Sgr : (_state.MouseX10 ? TerminalMouseProtocol.X10 : TerminalMouseProtocol.Vt200);
                    break;
                case 6:
                    _state.OriginMode = enabled;
                    _state.CursorRow = enabled ? _state.ScrollTop : 0;
                    _state.CursorColumn = 0;
                    _state.WrapPending = false;
                    break;
                case 7:
                    _state.AutoWrap = enabled;
                    break;
                case 2004:
                    _state.BracketedPaste = enabled;
                    break;
                case 1:
                    _state.ApplicationCursorKeys = enabled;
                    break;
            }
        }
    }

    private void SetModes(IReadOnlyList<int> parameters, bool enabled)
    {
        foreach (int param in parameters)
        {
            switch (param)
            {
                case 4:
                    _state.InsertMode = enabled;
                    break;
            }
        }
    }

    private void SavePrivateModes()
    {
        _savedPrivateModes = new PrivateModeSnapshot
        {
            AltBufferActive = _state.AltBufferActive,
            MouseMode = _state.MouseMode,
            MouseSgr = _state.MouseSgr,
            MouseProtocol = _state.MouseProtocol,
            MouseX10 = _state.MouseX10,
            OriginMode = _state.OriginMode,
            AutoWrap = _state.AutoWrap,
            ApplicationCursorKeys = _state.ApplicationCursorKeys,
            BracketedPaste = _state.BracketedPaste
        };
        _hasSavedPrivateModes = true;
    }

    private void RestorePrivateModes()
    {
        if (!_hasSavedPrivateModes)
        {
            return;
        }

        _state.AltBufferActive = _savedPrivateModes.AltBufferActive;
        _state.MouseMode = _savedPrivateModes.MouseMode;
        _state.MouseSgr = _savedPrivateModes.MouseSgr;
        _state.MouseProtocol = _savedPrivateModes.MouseProtocol;
        _state.MouseX10 = _savedPrivateModes.MouseX10;
        _state.OriginMode = _savedPrivateModes.OriginMode;
        _state.AutoWrap = _savedPrivateModes.AutoWrap;
        _state.ApplicationCursorKeys = _savedPrivateModes.ApplicationCursorKeys;
        _state.BracketedPaste = _savedPrivateModes.BracketedPaste;
    }

    private struct PrivateModeSnapshot
    {
        public bool AltBufferActive { get; init; }
        public TerminalMouseMode MouseMode { get; init; }
        public bool MouseSgr { get; init; }
        public TerminalMouseProtocol MouseProtocol { get; init; }
        public bool MouseX10 { get; init; }
        public bool OriginMode { get; init; }
        public bool AutoWrap { get; init; }
        public bool ApplicationCursorKeys { get; init; }
        public bool BracketedPaste { get; init; }
    }

    private sealed class TerminalHyperlinkStore
    {
        private readonly Dictionary<int, string> _links = new();
        private int _nextId = 1;

        public int Add(string url)
        {
            int id = _nextId++;
            _links[id] = url;
            return id;
        }

        public string? TryGet(int id)
        {
            return _links.TryGetValue(id, out string? value) ? value : null;
        }
    }
}
