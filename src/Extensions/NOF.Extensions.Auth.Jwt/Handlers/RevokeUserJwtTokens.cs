using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace NOF;

/// <summary>
/// Handler for revoking all user JWT tokens requests.
/// </summary>
public class RevokeUserJwtTokens : IRequestHandler<RevokeUserJwtTokensRequest, RevokeUserJwtTokensResponse>
{
    private readonly JwtOptions _options;
    private readonly ICacheService _cache;

    public RevokeUserJwtTokens(IOptions<JwtOptions> options, ICacheService cache)
    {
        _options = options.Value;
        _cache = cache;
    }

    public async Task<Result<RevokeUserJwtTokensResponse>> HandleAsync(RevokeUserJwtTokensRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var revokedData = new RevokedUserData(DateTime.UtcNow);
            var cacheKey = new RevokedUserCacheKey(request.UserId);

            await _cache.SetAsync(cacheKey, revokedData, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_options.RefreshTokenExpirationDays)
            }, cancellationToken);

            return Result.Success(new RevokeUserJwtTokensResponse(true));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, ex.Message);
        }
    }
}
