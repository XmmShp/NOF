using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;

namespace NOF.Sample.Application.RequestHandlers;

public class RemoveConfigFile : NOFSampleService.RemoveConfigFile
{
    private readonly DbContext _dbContext;
    private readonly ICacheService _cache;

    public RemoveConfigFile(DbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<Result> RemoveConfigFileAsync(RemoveConfigFileRequest request)
    {
        var cancellationToken = CancellationToken.None;
        var id = ConfigNodeId.Of(request.NodeId);
        var node = await _dbContext.Set<ConfigNode>().FindAsync([id], cancellationToken);

        if (node is null)
        {
            return Result.Fail("404", "Node not found.");
        }

        var fileName = ConfigFileName.Of(request.FileName);
        node.RemoveConfigFile(fileName);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 写后删：清除相关缓存
        await _cache.RemoveAsync(new ConfigNodeByIdCacheKey(id), cancellationToken);
        await _cache.RemoveAsync(new ConfigNodeByNameCacheKey(node.Name), cancellationToken);
        var version = DateTime.UtcNow.Ticks;
        await _cache.SetAsync(
            new ConfigNodeVersionCacheKey(id),
            version,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
            cancellationToken);

        return Result.Success();
    }
}

