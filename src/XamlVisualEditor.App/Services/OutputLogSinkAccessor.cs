using XamlVisualEditor.Core.Logging;

namespace XamlVisualEditor.App.Services;

public sealed class OutputLogSinkAccessor : IOutputLogSinkAccessor
{
    public IOutputLogSink? Sink { get; set; }
}
