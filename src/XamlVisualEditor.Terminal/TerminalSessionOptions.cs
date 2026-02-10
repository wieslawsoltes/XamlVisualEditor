using System.Collections.Generic;

namespace XamlVisualEditor.Terminal;

public sealed class TerminalSessionOptions
{
    public int Columns { get; set; } = 120;
    public int Rows { get; set; } = 40;
    public int ScrollbackLimit { get; set; } = 10000;
    public bool EnableSequenceLog { get; set; }
    public string? SequenceLogPath { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? Command { get; set; }
    public IReadOnlyList<string> Arguments { get; set; } = new List<string>();
    public IReadOnlyDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();
}
