namespace NOF;

/// <summary>
/// Constants for JWT authentication.
/// </summary>
public static class NOFJwtConstants
{
    /// <summary>
    /// JWT token type.
    /// </summary>
    public const string TokenType = "Bearer";

    /// <summary>
    /// Default algorithm for JWT signing.
    /// </summary>
    public const string Algorithm = "RS256";

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
}
