using System.Collections.Generic;

namespace XamlVisualEditor.Acp;

public sealed class AcpAgentProcessOptions
{
    public string FileName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
    public bool RedirectStandardError { get; set; } = true;
}
