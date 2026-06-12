using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

/// <summary>
/// <see cref="IRevokedRefreshTokenRepository"/> implementation backed by <see cref="ICacheService"/>.
/// </summary>
public class CacheRevokedRefreshTokenRepository : IRevokedRefreshTokenRepository
{
    private readonly ICacheService _cacheService;

    public CacheRevokedRefreshTokenRepository(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string tokenId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var cacheKey = new RevokedRefreshTokenCacheKey(tokenId);
        await _cacheService.SetAsync(cacheKey, new RevokedRefreshToken
        {
            TokenId = tokenId,
            ExpiresAtUtc = DateTime.UtcNow.Add(expiration)
        }, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var cacheKey = new RevokedRefreshTokenCacheKey(tokenId);
        var result = await _cacheService.GetAsync(cacheKey, cancellationToken);
        return result.HasValue && result.Value.ExpiresAtUtc > DateTime.UtcNow;
    }
}
