using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Sample.Application.CacheKeys;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class DeleteConfigNode : NOFSampleService.DeleteConfigNode
{
    private readonly IRepository<ConfigNode, ConfigNodeId> _configNodeRepository;
    private readonly IConfigNodeChildrenRepository _childrenRepository;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _uow;

    public DeleteConfigNode(
        IRepository<ConfigNode, ConfigNodeId> configNodeRepository,
        IConfigNodeChildrenRepository childrenRepository,
        ICacheService cache,
        IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _childrenRepository = childrenRepository;
        _cache = cache;
        _uow = uow;
    }

    public async Task<Result> DeleteConfigNodeAsync(DeleteConfigNodeRequest request)
    {
        var cancellationToken = CancellationToken.None;
        var id = ConfigNodeId.Of(request.Id);
        var node = await _configNodeRepository.FindAsync(id, cancellationToken);
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
        _configNodeRepository.Remove(node);
        await _uow.SaveChangesAsync(cancellationToken);

        // 写后删：清除相关缓存
        await _cache.RemoveAsync(new ConfigNodeByIdCacheKey(id), cancellationToken);
        await _cache.RemoveAsync(new ConfigNodeByNameCacheKey(node.Name), cancellationToken);

        return Result.Success();
    }
}



