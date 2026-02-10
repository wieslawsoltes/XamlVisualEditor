using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public interface IAcpAgentHostFactory
{
    Task<AcpAgentHost> StartAsync(AcpAgentProcessOptions options, CancellationToken ct);
}
