namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Options for issuing a refresh token.
/// </summary>
public sealed class JwtRefreshTokenOptions
{
    /// <summary>
    /// Gets or sets the refresh token expiration.
    /// </summary>
    public TimeSpan Expiration { get; set; }

    /// <summary>
    /// Gets or sets the refresh token claims.
    /// </summary>
    public JwtClaim[]? Claims { get; set; }
}
