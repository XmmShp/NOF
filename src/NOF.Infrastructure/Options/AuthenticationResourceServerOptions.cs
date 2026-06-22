namespace NOF.Infrastructure;

/// <summary>
/// Configuration options for JWT resource server validation.
/// </summary>
public class AuthenticationResourceServerOptions
{
    /// <summary>
    /// Gets or sets the accepted access token sources.
    /// </summary>
    public List<AuthenticationTokenSourceOptions> Sources { get; set; } = [new AuthenticationTokenSourceOptions()];

    /// <summary>
    /// Gets or sets the minimum interval between two JWKS refresh attempts.
    /// Default is 24 hours.
    /// </summary>
    public TimeSpan JwksRefreshInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the authorization server used to discover signing keys for token validation.
    /// </summary>
    public string AuthorizationServer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the configured authorization server metadata endpoint must use HTTPS.
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

    /// <summary>
    /// Gets or sets the client credentials used to obtain this service's own token before token exchange.
    /// </summary>
    public AuthenticationClientCredentialsOptions? TokenExchangeClient { get; set; }
}
