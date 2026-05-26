namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Represents an issued refresh token.
/// </summary>
public sealed class IssuedJwtRefreshToken
{
    /// <summary>
    /// Gets or sets the refresh token value.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// Gets or sets the expiration time of the refresh token.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }
}
