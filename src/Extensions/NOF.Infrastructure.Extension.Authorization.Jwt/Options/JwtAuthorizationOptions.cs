using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Configuration options for JWT authorization (OIDC resource server).
/// </summary>
public class JwtAuthorizationOptions
{
    /// <summary>
    /// Gets or sets the header name used to propagate the authorization token.
    /// Default is "Authorization".
    /// </summary>
    public string HeaderName { get; set; } = NOFInfrastructureCoreConstants.Transport.Headers.Authorization;

    /// <summary>
    /// Gets or sets the JWKS endpoint URL used to fetch signing keys for token validation.
    /// Used by both <see cref="HttpJwksProvider"/> and <see cref="RequestSenderJwksProvider"/>.
    /// </summary>
    public string JwksEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token type prefix (e.g., "Bearer").
    /// Default is "Bearer".
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets whether to require HTTPS for the authority URL. Default is true.
    /// Set to false only for development/testing scenarios.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets the expected issuer for token validation.
    /// If null, issuer validation uses the Authority value.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the expected audience for token validation.
    /// If null, audience validation is disabled.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets how long cached JWKS keys remain valid before a background refresh is attempted.
    /// Default is 30 days.
    /// </summary>
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromDays(30);
}
