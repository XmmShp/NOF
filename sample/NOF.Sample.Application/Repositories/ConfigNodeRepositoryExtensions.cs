using NOF.Abstraction;
using NOF.Domain;

namespace NOF.Sample.Application;

public static class ConfigNodeRepositoryExtensions
{
    public static Task<List<ConfigNode>> GetRootNodesAsync(
        this IRepository<ConfigNode, ConfigNodeId> repository,
        CancellationToken cancellationToken = default)
    {
        return AsyncHelper.FromSync(
            () => repository.AsNoTracking()
                .Where(n => n.ParentId == null)
                .ToList(),
            cancellationToken);
    }

    public static Task<ConfigNode?> GetNodeByIdAsync(
        this IRepository<ConfigNode, ConfigNodeId> repository,
        ConfigNodeId id,
        CancellationToken cancellationToken = default)
    {
        return AsyncHelper.FromSync(
            () => repository.AsNoTracking()
                .FirstOrDefault(n => n.Id == id),
            cancellationToken);
    }

    public static Task<ConfigNode?> GetNodeByNameAsync(
        this IRepository<ConfigNode, ConfigNodeId> repository,
        ConfigNodeName name,
        CancellationToken cancellationToken = default)
    {
        return AsyncHelper.FromSync(
            () => repository.AsNoTracking()
                .FirstOrDefault(node => node.Name == name),
            cancellationToken);
    }

    public static Task<ConfigNode?> FindByNameAsync(
        this IRepository<ConfigNode, ConfigNodeId> repository,
        ConfigNodeName name,
        ConfigNodeId? parentId = null,
        CancellationToken cancellationToken = default)
    {
        return AsyncHelper.FromSync(
            () => repository.AsNoTracking()
                .FirstOrDefault(node => node.Name == name && node.ParentId == parentId),
            cancellationToken);
    }

    public static Task<bool> ExistsByNameAsync(
        this IRepository<ConfigNode, ConfigNodeId> repository,
        ConfigNodeName name,
        CancellationToken cancellationToken = default)
    {
        return AsyncHelper.FromSync(
            () => repository.AsNoTracking().Any(node => node.Name == name),
            cancellationToken);
    }
}
