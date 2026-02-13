using NOF.Application;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Cache key for revoked refresh tokens.
/// </summary>
public sealed record RevokedRefreshTokenCacheKey(string TokenId) : CacheKey<bool>($"jwt:revoked_refresh:{TokenId}");
