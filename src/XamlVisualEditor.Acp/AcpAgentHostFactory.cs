using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed class AcpAgentHostFactory : IAcpAgentHostFactory
{
    public Task<AcpAgentHost> StartAsync(AcpAgentProcessOptions options, CancellationToken ct)
    {
        return AcpAgentHost.StartAsync(options, ct);
    }
}
