using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class DeleteConfigNode : NOFSampleService.DeleteConfigNode
{
    private readonly DbContext _dbContext;
    private readonly IConfigNodeChildrenRepository _childrenRepository;
    private readonly ICacheService _cache;

    public DeleteConfigNode(
        DbContext dbContext,
        IConfigNodeChildrenRepository childrenRepository,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _childrenRepository = childrenRepository;
        _cache = cache;
    }

    public override async Task<Result> HandleAsync(DeleteConfigNodeRequest request, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.Of(request.Id);
        var node = await _dbContext.FindAsync<ConfigNode>([id], cancellationToken);
        if (node is null)
        {
            return Result.Fail("404", "Node not found.");
        }

        // 检查是否有子节点
        var hasChildren = await _childrenRepository.HasChildrenAsync(id, cancellationToken);
        if (hasChildren)
        {
            return Result.Fail("400", "Cannot delete node with children.");
        }

        node.MarkAsDeleted();
        _dbContext.Set<ConfigNode>().Remove(node);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 写后删：清除相关缓存
        await _cache.RemoveAsync(new ConfigNodeByIdCacheKey(id), cancellationToken);
        await _cache.RemoveAsync(new ConfigNodeByNameCacheKey(node.Name), cancellationToken);

        return Result.Success();
    }
}
