namespace NOF;

/// <summary>
/// Constants for JWT authentication.
/// </summary>
public static class NOFJwtConstants
{
    /// <summary>
    /// Cache key prefix for revoked tokens.
    /// </summary>
    public const string RevokedTokenCachePrefix = "jwt:revoked:";

    /// <summary>
    /// Cache key prefix for revoked user tokens.
    /// </summary>
    public const string RevokedUserCachePrefix = "jwt:revoked_user:";

    /// <summary>
    /// Cache key for JWKS.
    /// </summary>
    public const string JwksCacheKey = "jwt:jwks";

    /// <summary>
    /// Cache key prefix for refresh tokens.
    /// </summary>
    public const string RefreshTokenCachePrefix = "refresh:";

    /// <summary>
    /// JWT token type.
    /// </summary>
    public const string TokenType = "Bearer";

    /// <summary>
    /// Default algorithm for JWT signing.
    /// </summary>
    public const string DefaultAlgorithm = "RS256";

    /// <summary>
    /// Default JWKS endpoint path.
    /// </summary>
    public const string DefaultJwksPath = "/.well-known/jwks.json";

    /// <summary>
    /// Claim types.
    /// </summary>
    public static class ClaimTypes
    {
        public const string JwtId = "jti";
        public const string Subject = "sub";
        public const string TenantId = "tenant_id";
        public const string Issuer = "iss";
        public const string Audience = "aud";
        public const string IssuedAt = "iat";
        public const string ExpiresAt = "exp";
        public const string Role = "role";
        public const string Permission = "permission";
    }

    /// <summary>
    /// Default expiration times.
    /// </summary>
    public static class Expiration
    {
        public static readonly TimeSpan DefaultAccessTokenExpiration = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan DefaultRefreshTokenExpiration = TimeSpan.FromDays(7);
        public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(1);
        public static readonly TimeSpan RevokedTokenCacheDuration = TimeSpan.FromDays(7);
    }
}
