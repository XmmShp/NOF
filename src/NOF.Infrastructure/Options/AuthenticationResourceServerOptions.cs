namespace NOF.Infrastructure;

/// <summary>
/// Configuration options for JWT resource server validation.
/// </summary>
public class AuthenticationResourceServerOptions
{

    /// <summary>
    /// Gets or sets the accepted access token header sources for the single configured authority.
    /// This is not a list of trusted issuers.
    /// </summary>
    public List<AuthenticationTokenSourceOptions> Sources { get; set; } = [new AuthenticationTokenSourceOptions()];

    /// <summary>
    /// Gets or sets the minimum interval between two JWKS refresh attempts.
    /// Default is 24 hours.
    /// </summary>
    public TimeSpan JwksRefreshInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the authorization server issuer URL used to discover metadata and signing keys.
    /// This must match the <c>issuer</c> value returned by the authorization server metadata document,
    /// for example <c>https://auth.example.com/oauth2</c>.
    /// </summary>
    public string AuthorizationServerIssuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the configured authorization server metadata endpoint must use HTTPS.
    /// Default is true.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets the expected issuer for token validation.
    /// If null, the issuer from the discovered authorization server metadata is used.
    /// Tokens are not authenticated if neither source supplies a non-empty issuer.
    /// </summary>
    public string? ExpectedIssuer { get; set; }

    /// <summary>
    /// Gets or sets the expected audience for token validation.
    /// If null, audience validation is disabled.
    /// Production resource servers should always configure an explicit audience.
    /// </summary>
    public string? Audience { get; set; }

}
