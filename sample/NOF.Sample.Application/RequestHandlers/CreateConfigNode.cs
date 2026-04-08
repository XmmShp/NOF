using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Sample.Application.CacheKeys;

namespace NOF.Sample.Application.RequestHandlers;

public class CreateConfigNode : NOFSampleService.CreateConfigNode
{
    private readonly IRepository<ConfigNode, ConfigNodeId> _configNodeRepository;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _uow;

    public CreateConfigNode(
        IRepository<ConfigNode, ConfigNodeId> configNodeRepository,
        ICacheService cache,
        IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _cache = cache;
        _uow = uow;
    }

    public async Task<Result> CreateConfigNodeAsync(CreateConfigNodeRequest request, CancellationToken cancellationToken)
    {
        var name = ConfigNodeName.Of(request.Name);
        var parentId = request.ParentId.HasValue ? ConfigNodeId.Of(request.ParentId.Value) : (ConfigNodeId?)null;

        if (await _configNodeRepository.ExistsByNameAsync(name, cancellationToken))
        {
            return Result.Fail("400", "Node with same name already exists.");
        }

        var node = ConfigNode.Create(name, parentId);
        _configNodeRepository.Add(node);
        await _uow.SaveChangesAsync(cancellationToken);

        // 写后删：清除相关缓存
        await _cache.RemoveAsync(new ConfigNodeByNameCacheKey(name), cancellationToken);
        var version = DateTime.UtcNow.Ticks;
        await _cache.SetAsync(new ConfigNodeVersionCacheKey(node.Id),
            version,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
            cancellationToken);

        return Result.Success();
    }
}




