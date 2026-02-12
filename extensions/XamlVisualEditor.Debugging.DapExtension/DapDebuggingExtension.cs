using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Debugging;
using XamlVisualEditor.Debugging.Dap;

namespace XamlVisualEditor.Debugging.DapExtension;

/// <summary>Registers the netcoredbg DAP debugger service.</summary>
public sealed class DapDebuggingExtension : IXveExtension
{
    private const string ServiceId = "debugger.netcoredbg";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        IDebuggerService service = new DapDebuggerService();
        context.DebuggerRegistry.Register(
            new DebuggerServiceRegistration(ServiceId, "netcoredbg (DAP)", service));
        return Task.CompletedTask;
    }
}
