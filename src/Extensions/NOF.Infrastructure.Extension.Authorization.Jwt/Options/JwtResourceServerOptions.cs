namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Configuration options for JWT resource server validation.
/// </summary>
public class JwtResourceServerOptions
{
    /// <summary>
    /// Gets or sets the accepted JWT token sources.
    /// </summary>
    public List<JwtResourceServerTokenSourceOptions> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the minimum interval between two JWKS refresh attempts.
    /// Default is 24 hours.
    /// </summary>
    public TimeSpan JwksRefreshInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the JWKS endpoint used to fetch signing keys for token validation.
    /// </summary>
    public string JwksEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the configured JWKS endpoint must use HTTPS.
    /// Default is true.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets the expected issuer for token validation.
    /// If null, issuer validation is disabled.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the expected audience for token validation.
    /// If null, audience validation is disabled.
    /// </summary>
    public string? Audience { get; set; }
}
