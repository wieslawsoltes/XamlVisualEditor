using System;

namespace XamlVisualEditor.Core;

public sealed class LanguageServiceSessionFaultedEventArgs : EventArgs
{
    public LanguageServiceSessionFaultedEventArgs(string languageId, Exception? error)
    {
        LanguageId = languageId;
        Error = error;
    }

    public string LanguageId { get; }

    public Exception? Error { get; }
}
