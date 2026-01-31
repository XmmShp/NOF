namespace NOF;

/// <summary>
/// Cache key for revoked tokens.
/// </summary>
public sealed record RevokedTokenCacheKey(string TokenId) : CacheKey<RevokedTokenData>($"jwt:revoked:{TokenId}");

/// <summary>
/// Data for revoked tokens.
/// </summary>
public sealed record RevokedTokenData(DateTime RevokedAt);

