using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;
using NOF.Abstraction;

namespace NOF.Sample.Application.RequestHandlers;

public class DeleteConfigNode : NOFSampleService.DeleteConfigNode
{
    private readonly DbContext _dbContext;
    private readonly ICacheService _cache;

    public DeleteConfigNode(
        DbContext dbContext,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public override async Task<RpcResult<Empty>> HandleAsync(DeleteConfigNodeRequest request, Context context, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.Of(request.Id);
        var node = await _dbContext.Set<ConfigNode>()
            .FirstOrDefaultAsync(configNode => configNode.Id == id, cancellationToken);
        if (node is null)
        {
            return Response("Node not found.", HttpTransportMetadata.Create(404));
        }

        // 检查是否有子节点
        var children = await _dbContext.Set<Entities.ConfigNodeChildren>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.NodeId == id, cancellationToken);
        var hasChildren = children?.HasChildren() ?? false;
        if (hasChildren)
        {
            return Response("Cannot delete node with children.", HttpTransportMetadata.Create(400));
        }

        node.MarkAsDeleted();
        _dbContext.Set<ConfigNode>().Remove(node);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 写后删：清除相关缓存
        await _cache.RemoveAsync(new ConfigNodeByIdCacheKey(id), cancellationToken);
        await _cache.RemoveAsync(new ConfigNodeByNameCacheKey(node.Name), cancellationToken);

        return Success(new Empty());
    }
}
