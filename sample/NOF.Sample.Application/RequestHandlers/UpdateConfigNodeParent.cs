using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;
using NOF.Abstraction;

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

    public override async Task<RpcResult<Empty>> HandleAsync(UpdateConfigNodeParentRequest request, NOFContext context, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.NodeId);
        var node = await _dbContext.Set<ConfigNode>()
            .FirstOrDefaultAsync(configNode => configNode.Id == nodeId, cancellationToken);

        if (node is null)
        {
            return Response("Node not found.", HttpTransportMetadata.Create(404));
        }

        var newParentId = request.NewParentId.HasValue
            ? ConfigNodeId.Of(request.NewParentId.Value)
            : (ConfigNodeId?)null;

        // Check whether the target parent exists (unless moving to root).
        if (newParentId.HasValue)
        {
            var parentNode = await _dbContext.Set<ConfigNode>()
                .FirstOrDefaultAsync(configNode => configNode.Id == newParentId.Value, cancellationToken);
            if (parentNode is null)
            {
                return Response("Target parent node not found.", HttpTransportMetadata.Create(404));
            }

            // Prevent cyclic parent relationship.
            if (await IsDescendant(nodeId, newParentId.Value, cancellationToken))
            {
                return Response("Cannot move a node under its descendant.", HttpTransportMetadata.Create(400));
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

        return Success(new Empty());
    }

    private async Task<bool> IsDescendant(ConfigNodeId ancestorId, ConfigNodeId nodeId, CancellationToken cancellationToken)
    {
        var current = await _dbContext.Set<ConfigNode>()
            .FirstOrDefaultAsync(configNode => configNode.Id == nodeId, cancellationToken);

        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId)
            {
                return true;
            }
            current = await _dbContext.Set<ConfigNode>()
                .FirstOrDefaultAsync(configNode => configNode.Id == current.ParentId.Value, cancellationToken);
        }

        return false;
    }
}
