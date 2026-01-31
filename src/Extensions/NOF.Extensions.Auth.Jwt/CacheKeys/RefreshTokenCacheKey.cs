namespace NOF;

/// <summary>
/// Cache key for refresh tokens.
/// </summary>
public sealed record RefreshTokenCacheKey(string TokenId) : CacheKey<RefreshTokenData>($"jwt:refresh:{TokenId}");
