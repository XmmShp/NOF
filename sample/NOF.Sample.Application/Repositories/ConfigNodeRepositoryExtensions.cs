using NOF.Domain;

namespace NOF.Sample.Application.Repositories;

public static class ConfigNodeRepositoryExtensions
{
    extension(IRepository<ConfigNode> set)
    {
        public IAsyncQueryable<ConfigNode> QueryRootNodes()
        {
            return set.AsNoTracking()
                .Where(n => n.ParentId == null)
                .AsAsyncQueryable();
        }

        public IAsyncQueryable<ConfigNode> QueryNodeById(ConfigNodeId id)
        {
            return set.AsNoTracking()
                .Where(n => n.Id == id)
                .AsAsyncQueryable();
        }

        public IAsyncQueryable<ConfigNode> QueryNodeByName(ConfigNodeName name)
        {
            return set.AsNoTracking()
                .Where(node => node.Name == name)
                .AsAsyncQueryable();
        }

        public Task<ConfigNode?> FindByNameAsync(
            ConfigNodeName name,
            ConfigNodeId? parentId = null,
            CancellationToken cancellationToken = default)
        {
            return set.AsNoTracking()
                .FirstOrDefaultAsync(node => node.Name == name && node.ParentId == parentId, cancellationToken);
        }

        public Task<bool> ExistsByNameAsync(
            ConfigNodeName name,
            CancellationToken cancellationToken = default)
        {
            return set.AsNoTracking().AnyAsync(node => node.Name == name, cancellationToken);
        }
    }
}
