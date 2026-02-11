namespace XamlVisualEditor.Extensions;

/// <summary>Provides extension logging.</summary>
public interface IExtensionLogger
{
    /// <summary>Logs an informational message.</summary>
    void Info(string message);

    /// <summary>Logs a warning message.</summary>
    void Warn(string message);

    /// <summary>Logs an error message.</summary>
    void Error(string message, Exception? exception = null);
}
