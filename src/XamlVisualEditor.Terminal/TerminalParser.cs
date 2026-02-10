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

                    emulator.HandleEscape((char)b);
                    _mode = ParserMode.Ground;
                    break;

                case ParserMode.Charset:
                    emulator.HandleCharsetSelect(_charsetIsG1, (char)b);
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
            }
        }
    }

    private enum ParserMode
    {
        Ground,
        Escape,
        Csi,
        Osc,
        Charset
    }

    private sealed class Utf8Decoder
    {
        private int _needed;
        private int _seen;
        private int _codePoint;

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
