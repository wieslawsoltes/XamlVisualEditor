using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public interface ISecretStore
{
    Task<string?> GetSecretAsync(string key, CancellationToken ct);

    Task SetSecretAsync(string key, string secret, CancellationToken ct);

    Task RemoveSecretAsync(string key, CancellationToken ct);
}
