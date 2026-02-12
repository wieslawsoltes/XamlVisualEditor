using System;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions.Debugging;

public sealed record DebugToolConsentRequest(
    string ToolId,
    string Version,
    string DownloadUrl,
    string InstallPath,
    string Message);

public interface IDebugToolInstaller
{
    Task<string?> EnsureNetcoredbgAsync(Func<DebugToolConsentRequest, Task<bool>> confirmAsync, CancellationToken ct = default);
    string? GetNetcoredbgPath();
}
