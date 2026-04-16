using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;

namespace NOF.Sample.Application.RequestHandlers;

public class CreateConfigNode : NOFSampleService.CreateConfigNode
{
    private readonly DbContext _dbContext;
    private readonly ICacheService _cache;

    public CreateConfigNode(
        DbContext dbContext,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public override async Task<Result> HandleAsync(CreateConfigNodeRequest request, CancellationToken cancellationToken)
    {
        var name = ConfigNodeName.Of(request.Name);
        var parentId = request.ParentId.HasValue ? ConfigNodeId.Of(request.ParentId.Value) : (ConfigNodeId?)null;

        if (await _dbContext.Set<ConfigNode>().ExistsByNameAsync(name, cancellationToken))
        {
            return Result.Fail("400", "Node with same name already exists.");
        }

        var node = ConfigNode.Create(name, parentId);
        _dbContext.Set<ConfigNode>().Add(node);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

