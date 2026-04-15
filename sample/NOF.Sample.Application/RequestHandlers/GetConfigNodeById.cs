using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.CacheKeys;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfigNodeById : NOFSampleService.GetConfigNodeById
{
    private readonly DbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetConfigNodeById(DbContext dbContext, ICacheService cache, IMapper mapper)
    {
        _dbContext = dbContext;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<Result<GetConfigNodeByIdResponse>> GetConfigNodeByIdAsync(GetConfigNodeByIdRequest request)
    {
        var cancellationToken = CancellationToken.None;
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

        var node = await _dbContext.Set<ConfigNode>().GetNodeByIdAsync(nodeId, cancellationToken);

        if (node is null)
        {
            return Result.Fail("404", "Config node not found.");
        }

        var dto = _mapper.Map<ConfigNode, ConfigNodeDto>(node);
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



