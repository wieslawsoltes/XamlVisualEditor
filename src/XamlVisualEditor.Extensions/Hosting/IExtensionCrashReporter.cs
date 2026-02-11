namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Records extension crash diagnostics.</summary>
public interface IExtensionCrashReporter
{
    /// <summary>Records a crash.</summary>
    Task RecordAsync(ExtensionCrashInfo crashInfo, CancellationToken cancellationToken);
}
