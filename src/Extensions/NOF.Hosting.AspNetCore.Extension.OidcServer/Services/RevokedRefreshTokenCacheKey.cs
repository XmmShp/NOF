using NOF.Application;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

/// <summary>
/// Cache key for revoked refresh tokens.
/// </summary>
public sealed record RevokedRefreshTokenCacheKey(string TokenId) : CacheKey<RevokedRefreshToken>($"jwt:revoked_refresh:{TokenId}");
