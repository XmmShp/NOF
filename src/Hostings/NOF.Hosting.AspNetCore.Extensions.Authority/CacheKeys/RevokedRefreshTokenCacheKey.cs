using NOF.Application;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Cache key for revoked refresh tokens.
/// </summary>
public sealed record RevokedRefreshTokenCacheKey(string TokenId) : CacheKey<bool>($"jwt:revoked_refresh:{TokenId}");
