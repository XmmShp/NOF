namespace NOF.Contract.Extension.Authentication;

/// <summary>
/// Options for issuing a refresh token.
/// </summary>
public sealed class RefreshTokenOptions
{
    /// <summary>
    /// Gets or sets the refresh token expiration.
    /// </summary>
    public TimeSpan Expiration { get; set; }

    /// <summary>
    /// Gets or sets the refresh token claims.
    /// </summary>
    public TokenClaim[]? Claims { get; set; }
}
