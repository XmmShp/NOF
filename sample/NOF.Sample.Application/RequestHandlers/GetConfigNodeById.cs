using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfigNodeById : NOFSampleService.GetConfigNodeById
{
    private readonly IDbContext _dbContext;
    private readonly ICacheService _cache;

    public GetConfigNodeById(IDbContext dbContext, ICacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public override async Task<Result<GetConfigNodeByIdResponse>> HandleAsync(GetConfigNodeByIdRequest request, Context context, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.Id);
        var cacheKey = new ConfigNodeByIdCacheKey(nodeId);

        var cachedValue = await _cache.GetAsync(cacheKey, cancellationToken: cancellationToken);
        if (cachedValue.HasValue)
        {
            return new GetConfigNodeByIdResponse
            {
                Node = cachedValue.Value
            };
        }

        var dto = await _dbContext.Set<ConfigNode>()
            .QueryNodeById(nodeId)
            .ProjectTo<ConfigNodeDto>()
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
        {
            return Result.Fail("404", "Config node not found.");
        }

        await _cache.SetAsync(
            cacheKey,
            dto,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) },
            cancellationToken);

        return new GetConfigNodeByIdResponse
        {
            Node = dto
        };
    }
}
