namespace NOF.Sample;

public interface IConfigNodeRepository : IRepository<ConfigNode>
{
    Task<ConfigNode?> FindByNameAsync(ConfigNodeName name, ConfigNodeId? parentId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(ConfigNodeName name, CancellationToken cancellationToken = default);
}
