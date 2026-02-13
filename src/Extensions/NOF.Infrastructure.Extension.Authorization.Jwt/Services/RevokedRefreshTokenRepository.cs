using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Repository for managing revoked refresh tokens.
/// Users can provide a custom implementation (e.g., database-backed).
/// </summary>
public interface IRevokedRefreshTokenRepository
{
    /// <summary>
    /// Marks a refresh token as revoked.
    /// </summary>
    /// <param name="tokenId">The token identifier (jti).</param>
    /// <param name="expiration">How long the revocation record should be retained.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeAsync(string tokenId, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a refresh token has been revoked.
    /// </summary>
    /// <param name="tokenId">The token identifier (jti).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the token has been revoked; otherwise <c>false</c>.</returns>
    Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IRevokedRefreshTokenRepository"/> implementation backed by <see cref="ICacheService"/>.
/// Because <see cref="ICacheService"/> is scoped, this implementation creates a scope on each call.
/// </summary>
public class CacheRevokedRefreshTokenRepository : IRevokedRefreshTokenRepository
{
    private readonly IServiceProvider _serviceProvider;

    public CacheRevokedRefreshTokenRepository(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string tokenId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var cacheKey = new RevokedRefreshTokenCacheKey(tokenId);
        await cache.SetAsync(cacheKey, true, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var cacheKey = new RevokedRefreshTokenCacheKey(tokenId);
        var result = await cache.GetAsync(cacheKey, cancellationToken);
        return result.HasValue;
    }
}
