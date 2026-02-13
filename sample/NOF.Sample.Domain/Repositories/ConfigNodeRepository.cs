using NOF.Domain;

namespace NOF.Sample;

public interface IConfigNodeRepository : IRepository<ConfigNode, ConfigNodeId>
{
    Task<ConfigNode?> FindByNameAsync(ConfigNodeName name, ConfigNodeId? parentId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(ConfigNodeName name, CancellationToken cancellationToken = default);
}
