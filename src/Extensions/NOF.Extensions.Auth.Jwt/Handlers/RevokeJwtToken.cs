using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace NOF;

/// <summary>
/// Handler for revoking JWT token requests.
/// </summary>
public class RevokeJwtToken : IRequestHandler<RevokeJwtTokenRequest, RevokeJwtTokenResponse>
{
    private readonly JwtOptions _options;
    private readonly ICacheService _cache;

    public RevokeJwtToken(IOptions<JwtOptions> options, ICacheService cache)
    {
        _options = options.Value;
        _cache = cache;
    }

    public async Task<Result<RevokeJwtTokenResponse>> HandleAsync(RevokeJwtTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var revokedData = new RevokedTokenData(DateTime.UtcNow);
            var cacheKey = new RevokedTokenCacheKey(request.TokenId);
            
            await _cache.SetAsync(cacheKey, revokedData, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_options.RefreshTokenExpirationDays)
            }, cancellationToken);

            return Result.Success(new RevokeJwtTokenResponse(true));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, ex.Message);
        }
    }
}
