namespace NOF;

/// <summary>
/// Cache key for revoked refresh tokens.
/// </summary>
public sealed record RevokedRefreshTokenCacheKey(string TokenId) : CacheKey<bool>($"jwt:revoked_refresh:{TokenId}");

