using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;
using NOF.Abstraction;

namespace NOF.Sample.Application.RequestHandlers;

public class AddOrUpdateConfigFile : NOFSampleService.AddOrUpdateConfigFile
{
    private readonly DbContext _dbContext;
    private readonly ICacheService _cache;

    public AddOrUpdateConfigFile(DbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public override async Task<RpcResult<Empty>> HandleAsync(AddOrUpdateConfigFileRequest request, Context context, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.Of(request.NodeId);
        var node = await _dbContext.Set<ConfigNode>()
            .FirstOrDefaultAsync(configNode => configNode.Id == id, cancellationToken);

        if (node is null)
        {
            return Response("Node not found.", HttpTransportMetadata.Create(404));
        }

        var fileName = ConfigFileName.Of(request.FileName);
        var content = ConfigContent.Of(request.Content);

        node.AddOrUpdateConfigFile(fileName, content);
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

        return Success(Empty.Instance);
    }
}
