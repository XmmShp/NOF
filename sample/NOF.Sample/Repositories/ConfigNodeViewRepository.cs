using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NOF;
using NOF.Sample;
using NOF.Sample.Application.CacheKeys;
using NOF.Sample.Application.Entities;
using NOF.Sample.Application.Repositories;

namespace ConfigurationCenter;

[AutoInject(Lifetime.Scoped)]
public class ConfigNodeViewRepository : IConfigNodeViewRepository
{
    private readonly ConfigurationDbContext _dbContext;
    private readonly ICacheService _cache;

    public ConfigNodeViewRepository(ConfigurationDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<ConfigNodeDto?> GetByIdAsync(ConfigNodeId id, CancellationToken cancellationToken = default)
    {
        var cacheKey = new ConfigNodeByIdCacheKey(id);
        return await GetOrSetCacheAsync(cacheKey, async () =>
        {
            var node = await _dbContext.ConfigNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

            return node is null ? null : MapToDto(node);
        }, cancellationToken);
    }

    public async Task<ConfigNodeDto?> GetByNameAsync(ConfigNodeName name, CancellationToken cancellationToken = default)
    {
        var cacheKey = new ConfigNodeByNameCacheKey(name);
        return await GetOrSetCacheAsync(cacheKey, async () =>
        {
            var node = await _dbContext.ConfigNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Name == name, cancellationToken);

            return node is null ? null : MapToDto(node);
        }, cancellationToken);
    }

    public async Task<List<ConfigNodeDto>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var rootNodes = await _dbContext.ConfigNodes
            .AsNoTracking()
            .Where(n => n.ParentId == null)
            .ToListAsync(cancellationToken);

        return rootNodes.Select(MapToDto).ToList();
    }

    public async Task<ConfigNodeChildren?> GetChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default)
    {
        var children = await _dbContext.ConfigNodeChildren
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.NodeId == id, cancellationToken);

        return children ?? null;
    }

    public async Task<bool> HasChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default)
    {
        var children = await _dbContext.ConfigNodeChildren
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.NodeId == id, cancellationToken);

        return children is not null && children.HasChildren();
    }

    public async Task AddChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default)
    {
        // 如果有父节点，更新父节点的子节点列表
        if (parentId.HasValue)
        {
            var parentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == parentId.Value, cancellationToken);

            if (parentChildren is null)
            {
                // 父节点还没有子节点记录，创建一个
                parentChildren = ConfigNodeChildren.Create(parentId.Value);
                _dbContext.ConfigNodeChildren.Add(parentChildren);
            }

            parentChildren.AddChild(nodeId);
        }

        // 为新节点创建一个空的子节点记录
        var newNodeChildren = ConfigNodeChildren.Create(nodeId);
        _dbContext.ConfigNodeChildren.Add(newNodeChildren);
    }

    public async Task RemoveChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default)
    {
        // 删除节点的子节点记录
        var nodeChildren = await _dbContext.ConfigNodeChildren
            .FirstOrDefaultAsync(c => c.NodeId == nodeId, cancellationToken);

        if (nodeChildren is not null)
        {
            _dbContext.ConfigNodeChildren.Remove(nodeChildren);
        }

        // 如果有父节点，从父节点的子节点列表中移除
        if (parentId.HasValue)
        {
            var parentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == parentId.Value, cancellationToken);

            parentChildren?.RemoveChild(nodeId);
        }
    }

    public async Task UpdateChildNodeParentAsync(ConfigNodeId nodeId, ConfigNodeId? oldParentId, ConfigNodeId? newParentId, CancellationToken cancellationToken = default)
    {
        // 从旧父节点的子节点列表中移除
        if (oldParentId.HasValue)
        {
            var oldParentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == oldParentId.Value, cancellationToken);

            oldParentChildren?.RemoveChild(nodeId);
        }

        // 添加到新父节点的子节点列表
        if (newParentId.HasValue)
        {
            var newParentChildren = await _dbContext.ConfigNodeChildren
                .FirstOrDefaultAsync(c => c.NodeId == newParentId.Value, cancellationToken);

            if (newParentChildren is null)
            {
                // 新父节点还没有子节点记录，创建一个
                newParentChildren = ConfigNodeChildren.Create(newParentId.Value);
                _dbContext.ConfigNodeChildren.Add(newParentChildren);
            }

            newParentChildren.AddChild(nodeId);
        }
    }

    private async Task<ConfigNodeDto?> GetOrSetCacheAsync(CacheKey<ConfigNodeDto> cacheKey, Func<Task<ConfigNodeDto?>> queryFn, CancellationToken cancellationToken)
    {
        var value = await _cache.GetAsync(cacheKey, cancellationToken: cancellationToken);
        if (value.HasValue)
        {
            return value.Value;
        }

        var dto = await queryFn();
        if (dto is not null)
        {
            await _cache.SetAsync(
                cacheKey,
                dto,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) },
                cancellationToken);
        }

        return dto;
    }

    private static ConfigNodeDto MapToDto(ConfigNode node)
    {
        return new ConfigNodeDto(
            node.Id.Value,
            node.ParentId?.Value,
            node.Name.Value,
            node.ActiveFileName?.Value,
            node.ConfigFiles.Select(f => new ConfigFileDto(f.Name.Value, f.Content.Value)).ToList()
        );
    }
}
