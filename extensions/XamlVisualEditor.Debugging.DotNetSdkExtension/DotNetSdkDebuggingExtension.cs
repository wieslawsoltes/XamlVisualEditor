using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Debugging.Dap;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Debugging;

namespace XamlVisualEditor.Debugging.DotNetSdkExtension;

/// <summary>Registers the .NET SDK debugger service.</summary>
public sealed class DotNetSdkDebuggingExtension : IXveExtension
{
    private const string ServiceId = "debugger.dotnet.sdk";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        IDebuggerService service = new DapDebuggerService(adapterId: "coreclr");
        IDebuggerAdapterLocator locator = new DotNetSdkAdapterLocator();
        context.DebuggerRegistry.Register(
            new DebuggerServiceRegistration(ServiceId, ".NET SDK Debugger (vsdbg)", service, locator),
            makeDefault: true);
        return Task.CompletedTask;
    }
}
