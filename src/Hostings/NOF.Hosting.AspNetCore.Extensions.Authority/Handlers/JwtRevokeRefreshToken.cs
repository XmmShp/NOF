using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Handler for revoking JWT refresh token requests.
/// This handler only revokes refresh tokens using their TokenId (jti).
/// Access tokens are short-lived and do not need revocation support.
/// </summary>
public class JwtRevokeRefreshToken : IRequestHandler<JwtRevokeRefreshTokenRequest>
{
    private readonly ICacheService _cache;

    public JwtRevokeRefreshToken(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<Result> HandleAsync(JwtRevokeRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = new RevokedRefreshTokenCacheKey(request.TokenId);

            await _cache.SetAsync(cacheKey, true, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = request.Expiration
            }, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"An unexpected error occurred: {ex.Message}");
        }
    }
}
