using Microsoft.EntityFrameworkCore;
using NOF.Sample.Application.Entities;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Repositories;

public class ConfigNodeChildrenRepository : IConfigNodeChildrenRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public ConfigNodeChildrenRepository(ConfigurationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ConfigNodeChildren?> GetChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConfigNodeChildren
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.NodeId == id, cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default)
    {
        var children = await _dbContext.ConfigNodeChildren
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.NodeId == id, cancellationToken);
        return children?.HasChildren() ?? false;
    }

    public async Task AddChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default)
    {
        if (parentId.HasValue)
        {
            var parentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == parentId.Value, cancellationToken);

            if (parentChildren is null)
            {
                parentChildren = ConfigNodeChildren.Create(parentId.Value);
                _dbContext.ConfigNodeChildren.Add(parentChildren);
            }

            parentChildren.AddChild(nodeId);
        }

        var newNodeChildren = ConfigNodeChildren.Create(nodeId);
        _dbContext.ConfigNodeChildren.Add(newNodeChildren);
    }

    public async Task RemoveChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default)
    {
        if (parentId.HasValue)
        {
            var parentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == parentId.Value, cancellationToken);
            parentChildren?.RemoveChild(nodeId);
        }

        var nodeChildren = await _dbContext.ConfigNodeChildren
            .FirstOrDefaultAsync(c => c.NodeId == nodeId, cancellationToken);

        if (nodeChildren is not null)
        {
            _dbContext.ConfigNodeChildren.Remove(nodeChildren);
        }
    }

    public async Task UpdateChildNodeParentAsync(ConfigNodeId nodeId, ConfigNodeId? oldParentId, ConfigNodeId? newParentId, CancellationToken cancellationToken = default)
    {
        if (oldParentId.HasValue)
        {
            var oldParentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == oldParentId.Value, cancellationToken);
            oldParentChildren?.RemoveChild(nodeId);
        }

        if (newParentId.HasValue)
        {
            var newParentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == newParentId.Value, cancellationToken);

            if (newParentChildren is null)
            {
                newParentChildren = ConfigNodeChildren.Create(newParentId.Value);
                _dbContext.ConfigNodeChildren.Add(newParentChildren);
            }

            newParentChildren.AddChild(nodeId);
        }
    }
}
