using System;
using System.Collections.Generic;
using System.Text;

namespace XamlVisualEditor.Terminal;

internal sealed class TerminalParser
{
    private ParserMode _mode = ParserMode.Ground;
    private readonly List<int> _params = new();
    private char _privatePrefix;
    private readonly List<byte> _oscBytes = new();
    private bool _oscEscaped;
    private readonly Utf8Decoder _utf8 = new();
    private bool _charsetIsG1;
    private bool _hashSequence;
    private bool _percentSequence;
    private bool _ignoreEscaped;

    public void Process(ReadOnlySpan<byte> data, TerminalEmulator emulator)
    {
        int index = 0;
        while (index < data.Length)
        {
            byte b = data[index++];
            switch (_mode)
            {
                case ParserMode.Ground:
                    if (b == 0x1B)
                    {
                        _mode = ParserMode.Escape;
                        _utf8.Reset();
                        continue;
                    }

                    if (b < 0x20)
                    {
                        emulator.HandleControl(b);
                        continue;
                    }

                    if (!emulator.State.Utf8Mode)
                    {
                        if (b >= 0x80 && b <= 0x9F && HandleC1(b, emulator))
                        {
                            continue;
                        }

                        emulator.WriteRune(new Rune(b));
                        break;
                    }

                    if (_utf8.HasPending)
                    {
                        if (b >= 0x80 && b <= 0xBF)
                        {
                            if (_utf8.TryDecode(b, out Rune pendingRune))
                            {
                                emulator.WriteRune(pendingRune);
                            }
                            break;
                        }

                        _utf8.Reset();
                    }

                    if (b >= 0x80 && b <= 0x9F && HandleC1(b, emulator))
                    {
                        continue;
                    }

                    if (_utf8.TryDecode(b, out Rune rune))
                    {
                        emulator.WriteRune(rune);
                    }
                    break;

                case ParserMode.Escape:
                    if (b == (byte)'[')
                    {
                        _mode = ParserMode.Csi;
                        _params.Clear();
                        _privatePrefix = '\0';
                        continue;
                    }

                    if (b == (byte)']')
                    {
                        _mode = ParserMode.Osc;
                        _oscBytes.Clear();
                        _oscEscaped = false;
                        continue;
                    }

                    if (b == (byte)'(' || b == (byte)')')
                    {
                        _charsetIsG1 = b == (byte)')';
                        _mode = ParserMode.Charset;
                        continue;
                    }

                    if (b == (byte)'#')
                    {
                        _hashSequence = true;
                        _mode = ParserMode.EscapeHash;
                        continue;
                    }

                    if (b == (byte)'%')
                    {
                        _percentSequence = true;
                        _mode = ParserMode.EscapePercent;
                        continue;
                    }

                    emulator.HandleEscape((char)b);
                    _mode = ParserMode.Ground;
                    break;

                case ParserMode.Charset:
                    emulator.HandleCharsetSelect(_charsetIsG1, (char)b);
                    _mode = ParserMode.Ground;
                    break;

                case ParserMode.EscapeHash:
                    if (_hashSequence)
                    {
                        emulator.HandleEscapeHash((char)b);
                    }
                    _hashSequence = false;
                    _mode = ParserMode.Ground;
                    break;

                case ParserMode.EscapePercent:
                    if (_percentSequence)
                    {
                        emulator.HandleEscapePercent((char)b);
                        _utf8.Reset();
                    }
                    _percentSequence = false;
                    _mode = ParserMode.Ground;
                    break;

                case ParserMode.Csi:
                    if (b == (byte)'?' || b == (byte)'>' || b == (byte)'!')
                    {
                        _privatePrefix = (char)b;
                        continue;
                    }

                    if (b >= '0' && b <= '9')
                    {
                        int value = b - '0';
                        if (_params.Count == 0)
                        {
                            _params.Add(value);
                        }
                        else
                        {
                            int last = _params[^1];
                            _params[^1] = last * 10 + value;
                        }
                        continue;
                    }

                    if (b == (byte)';')
                    {
                        if (_params.Count == 0)
                        {
                            _params.Add(0);
                        }
                        _params.Add(0);
                        continue;
                    }

                    if (b >= 0x40 && b <= 0x7E)
                    {
                        emulator.HandleCsi((char)b, _params, _privatePrefix);
                        _params.Clear();
                        _privatePrefix = '\0';
                        _mode = ParserMode.Ground;
                    }
                    break;

                case ParserMode.Osc:
                    if (b == 0x9C)
                    {
                        emulator.HandleOsc(Encoding.UTF8.GetString(_oscBytes.ToArray()));
                        _oscBytes.Clear();
                        _mode = ParserMode.Ground;
                        break;
                    }

                    if (_oscEscaped)
                    {
                        if (b == (byte)'\\')
                        {
                            emulator.HandleOsc(Encoding.UTF8.GetString(_oscBytes.ToArray()));
                            _oscBytes.Clear();
                            _oscEscaped = false;
                            _mode = ParserMode.Ground;
                            break;
                        }

                        _oscBytes.Add(0x1B);
                        _oscEscaped = false;
                    }

                    if (b == 0x1B)
                    {
                        _oscEscaped = true;
                        break;
                    }

                    if (b == 0x07)
                    {
                        emulator.HandleOsc(Encoding.UTF8.GetString(_oscBytes.ToArray()));
                        _oscBytes.Clear();
                        _mode = ParserMode.Ground;
                        break;
                    }

                    _oscBytes.Add(b);
                    break;
                case ParserMode.Ignore:
                    if (_ignoreEscaped)
                    {
                        if (b == (byte)'\\')
                        {
                            _mode = ParserMode.Ground;
                        }
                        _ignoreEscaped = false;
                        break;
                    }

                    if (b == 0x1B)
                    {
                        _ignoreEscaped = true;
                        break;
                    }

                    if (b == 0x9C)
                    {
                        _mode = ParserMode.Ground;
                    }
                    break;
            }
        }
    }

    private bool HandleC1(byte b, TerminalEmulator emulator)
    {
        switch (b)
        {
            case 0x84:
                emulator.HandleControl(0x0A);
                return true;
            case 0x85:
                emulator.HandleControl(0x0A);
                emulator.HandleControl(0x0D);
                return true;
            case 0x88:
                emulator.HandleEscape('H');
                return true;
            case 0x8D:
                emulator.HandleEscape('M');
                return true;
            case 0x90:
            case 0x98:
            case 0x9E:
            case 0x9F:
                _ignoreEscaped = false;
                _mode = ParserMode.Ignore;
                return true;
            case 0x9B:
                _mode = ParserMode.Csi;
                _params.Clear();
                _privatePrefix = '\0';
                return true;
            case 0x9C:
                return true;
            case 0x9D:
                _mode = ParserMode.Osc;
                _oscBytes.Clear();
                _oscEscaped = false;
                return true;
            default:
                return false;
        }
    }

    private enum ParserMode
    {
        Ground,
        Escape,
        Csi,
        Osc,
        Charset,
        EscapeHash,
        EscapePercent,
        Ignore
    }

    private sealed class Utf8Decoder
    {
        private int _needed;
        private int _seen;
        private int _codePoint;

        public bool HasPending => _needed != 0;

        public void Reset()
        {
            _needed = 0;
            _seen = 0;
            _codePoint = 0;
        }

        public bool TryDecode(byte b, out Rune rune)
        {
            rune = default;

            if (_needed == 0)
            {
                if (b < 0x80)
                {
                    rune = new Rune(b);
                    return true;
                }

                if (b >= 0xC2 && b <= 0xDF)
                {
                    _needed = 2;
                    _codePoint = b & 0x1F;
                }
                else if (b >= 0xE0 && b <= 0xEF)
                {
                    _needed = 3;
                    _codePoint = b & 0x0F;
                }
                else if (b >= 0xF0 && b <= 0xF4)
                {
                    _needed = 4;
                    _codePoint = b & 0x07;
                }
                else
                {
                    Reset();
                }

                _seen = 1;
                return false;
            }

            if (b < 0x80 || b > 0xBF)
            {
                Reset();
                return false;
            }

            _codePoint = (_codePoint << 6) | (b & 0x3F);
            _seen++;

            if (_seen == _needed)
            {
                if (Rune.TryCreate(_codePoint, out rune))
                {
                    Reset();
                    return true;
                }

                Reset();
            }

            return false;
        }
    }
}
