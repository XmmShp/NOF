using Microsoft.EntityFrameworkCore;

namespace NOF.Sample.Application;

public static class ConfigNodeRepositoryExtensions
{
    extension(DbSet<ConfigNode> set)
    {
        public Task<List<ConfigNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
        {
            return set.AsNoTracking()
                .Where(n => n.ParentId == null)
                .ToListAsync(cancellationToken);
        }

        public Task<ConfigNode?> GetNodeByIdAsync(
            ConfigNodeId id,
            CancellationToken cancellationToken = default)
        {
            return set.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        }

        public Task<ConfigNode?> GetNodeByNameAsync(
            ConfigNodeName name,
            CancellationToken cancellationToken = default)
        {
            return set.AsNoTracking()
                .FirstOrDefaultAsync(node => node.Name == name, cancellationToken);
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
