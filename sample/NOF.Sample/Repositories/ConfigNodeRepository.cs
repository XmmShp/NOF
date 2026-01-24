using Microsoft.EntityFrameworkCore;
using NOF;

namespace NOF.Sample.Repositories;

[AutoInject(Lifetime.Scoped)]
public class ConfigNodeRepository : EFCoreRepository<ConfigNode>, IConfigNodeRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public ConfigNodeRepository(ConfigurationDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ConfigNode?> FindByNameAsync(ConfigNodeName name, ConfigNodeId? parentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConfigNodes.FirstOrDefaultAsync(n => n.Name == name && n.ParentId == parentId, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(ConfigNodeName name, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConfigNodes.AnyAsync(n => n.Name == name, cancellationToken);
    }
}
