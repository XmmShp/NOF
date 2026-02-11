using System.ComponentModel.DataAnnotations;

namespace NOF;

/// <summary>
/// Configuration options for the JWT client (OIDC resource server).
/// </summary>
public class JwtClientOptions
{
    /// <summary>
    /// Gets or sets the authority URL (e.g., https://auth.example.com).
    /// The JWKS endpoint will be resolved as {Authority}/.well-known/jwks.json.
    /// </summary>
    [Required(ErrorMessage = "Authority is required.")]
    public string Authority { get; set; } = string.Empty;

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
