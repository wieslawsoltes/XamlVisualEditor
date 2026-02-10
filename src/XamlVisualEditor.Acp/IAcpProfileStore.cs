using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public interface IAcpProfileStore
{
    Task<IReadOnlyList<AcpProfile>> LoadAsync(CancellationToken ct);

    Task SaveAsync(IReadOnlyList<AcpProfile> profiles, CancellationToken ct);
}
