using NOF.Abstraction;
using NOF.Domain;

namespace NOF.Sample.Application;

public static class ConfigNodeRepositoryExtensions
{
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
