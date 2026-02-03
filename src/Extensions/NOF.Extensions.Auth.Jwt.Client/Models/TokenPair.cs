namespace NOF;

/// <summary>
/// Represents a pair of access and refresh tokens.
/// </summary>
public class TokenPair
{
    /// <summary>
    /// Gets or sets the JWT access token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expiration time of the access token.
    /// </summary>
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the expiration time of the refresh token.
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the token type.
    /// </summary>
    public string TokenType { get; set; } = NOFJwtConstants.TokenType;
}

/// <summary>
/// Represents JWT token claims.
/// </summary>
public class JwtClaims
{
    /// <summary>
    /// Gets or sets the unique identifier for the token.
    /// </summary>
    public string Jti { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject/user identifier.
    /// </summary>
    public string Sub { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of roles.
    /// </summary>
    public List<string>? Roles { get; set; }

    /// <summary>
    /// Gets or sets the list of permissions.
    /// </summary>
    public List<string>? Permissions { get; set; }

    /// <summary>
    /// Gets or sets additional custom claims.
    /// </summary>
    public Dictionary<string, string> CustomClaims { get; set; } = new();

    /// <summary>
    /// Gets or sets the issued at time.
    /// </summary>
    public DateTime Iat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the not before time.
    /// </summary>
    public DateTime? Nbf { get; set; }

    /// <summary>
    /// Gets or sets the expiration time.
    /// </summary>
    public DateTime Exp { get; set; }

    /// <summary>
    /// Gets or sets the issuer.
    /// </summary>
    public string Iss { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audience.
    /// </summary>
    public string Aud { get; set; } = string.Empty;
}
