namespace NOF;

/// <summary>
/// Cache key for revoked user tokens.
/// </summary>
public sealed record RevokedUserCacheKey(string UserId) : CacheKey<RevokedUserData>($"jwt:revoked_user:{UserId}");

/// <summary>
/// Data for revoked user tokens.
/// </summary>
public sealed record RevokedUserData(DateTime RevokedAt);

