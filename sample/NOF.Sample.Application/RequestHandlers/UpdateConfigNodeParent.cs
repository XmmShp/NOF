using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;

namespace NOF.Sample.Application.RequestHandlers;

public class UpdateConfigNodeParent : NOFSampleService.UpdateConfigNodeParent
{
    private readonly DbContext _dbContext;
    private readonly ICacheService _cache;

    public UpdateConfigNodeParent(
        DbContext dbContext,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public override async Task<Result> HandleAsync(UpdateConfigNodeParentRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.NodeId);
        var node = await _dbContext.FindAsync<ConfigNode>([nodeId], cancellationToken);

        if (node is null)
        {
            return Result.Fail("404", "Node not found.");
        }

        var newParentId = request.NewParentId.HasValue
            ? ConfigNodeId.Of(request.NewParentId.Value)
            : (ConfigNodeId?)null;

        // Check whether the target parent exists (unless moving to root).
        if (newParentId.HasValue)
        {
            var parentNode = await _dbContext.FindAsync<ConfigNode>([newParentId.Value], cancellationToken);
            if (parentNode is null)
            {
                return Result.Fail("404", "Target parent node not found.");
            }

            // Prevent cyclic parent relationship.
            if (await IsDescendant(nodeId, newParentId.Value, cancellationToken))
            {
                return Result.Fail("400", "Cannot move a node under its descendant.");
            }
        }

        node.UpdateParent(newParentId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 写后删：清除相关缓存
        await _cache.RemoveAsync(new ConfigNodeByIdCacheKey(nodeId), cancellationToken);
        var version = DateTime.UtcNow.Ticks;
        await _cache.SetAsync(
            new ConfigNodeVersionCacheKey(nodeId),
            version,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
            cancellationToken);

        return Result.Success();
    }

    private async Task<bool> IsDescendant(ConfigNodeId ancestorId, ConfigNodeId nodeId, CancellationToken cancellationToken)
    {
        var current = await _dbContext.FindAsync<ConfigNode>([nodeId], cancellationToken);

        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId)
            {
                return true;
            }
            current = await _dbContext.FindAsync<ConfigNode>([current.ParentId.Value], cancellationToken);
        }

        return false;
    }
}
