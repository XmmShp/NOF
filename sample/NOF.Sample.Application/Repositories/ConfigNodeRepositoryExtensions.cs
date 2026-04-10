using NOF.Abstraction;
using NOF.Domain;

namespace NOF.Sample.Application;

public static class ConfigNodeRepositoryExtensions
{
    extension(IRepository<ConfigNode, ConfigNodeId> repository)
    {
        public Task<List<ConfigNode>> GetRootNodesAsync(
        CancellationToken cancellationToken = default)
        {
            return AsyncHelper.FromSync(
                () => repository.AsNoTracking()
                    .Where(n => n.ParentId == null)
                    .ToList(),
                cancellationToken);
        }

        public Task<ConfigNode?> GetNodeByIdAsync(
            ConfigNodeId id,
            CancellationToken cancellationToken = default)
        {
            return AsyncHelper.FromSync(
                () => repository.AsNoTracking()
                    .FirstOrDefault(n => n.Id == id),
                cancellationToken);
        }

        public Task<ConfigNode?> GetNodeByNameAsync(
            ConfigNodeName name,
            CancellationToken cancellationToken = default)
        {
            return AsyncHelper.FromSync(
                () => repository.AsNoTracking()
                    .FirstOrDefault(node => node.Name == name),
                cancellationToken);
        }

        public Task<ConfigNode?> FindByNameAsync(
            ConfigNodeName name,
            ConfigNodeId? parentId = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncHelper.FromSync(
                () => repository.AsNoTracking()
                    .FirstOrDefault(node => node.Name == name && node.ParentId == parentId),
                cancellationToken);
        }

        public Task<bool> ExistsByNameAsync(
            ConfigNodeName name,
            CancellationToken cancellationToken = default)
        {
            return AsyncHelper.FromSync(
                () => repository.AsNoTracking().Any(node => node.Name == name),
                cancellationToken);
        }
    }
}
