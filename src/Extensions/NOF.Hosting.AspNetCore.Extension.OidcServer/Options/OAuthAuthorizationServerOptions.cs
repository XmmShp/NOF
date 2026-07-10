using System.ComponentModel.DataAnnotations;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

/// <summary>
/// Configuration options for the built-in OAuth/OIDC authorization server.
/// </summary>
public sealed class OAuthAuthorizationServerOptions
{
    /// <summary>
    /// Gets or sets the canonical issuer URL published in metadata and written into issued tokens.
    /// This should be the final issuer identifier, for example <c>https://auth.example.com/oauth2</c>.
    /// <see cref="PathBase"/> controls route mapping only and is not appended to this value automatically.
    /// </summary>
    [Required(ErrorMessage = "Issuer is required.")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the route prefix used when mapping the local authorization server endpoints.
    /// This does not change <see cref="Issuer"/>.
    /// </summary>
    public string PathBase { get; set; } = "/oauth2";

    public string AccessTokenAudience { get; set; } = "nof-app";

    [Range(2048, 4096, ErrorMessage = "KeySize must be between 2048 and 4096.")]
    public int KeySize { get; set; } = 2048;

    [Range(0, 10, ErrorMessage = "RetiredKeyRetentionCount must be between 0 and 10.")]
    public int RetiredKeyRetentionCount { get; set; } = 2;

    public string SigningKeyEncryptionKey { get; set; } = string.Empty;

    public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan RevokedRefreshTokenCleanupInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan SigningKeyCleanupInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RevokedSigningKeyRetention { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan AccessTokenExpiration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenExpiration { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan AuthorizationCodeExpiration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan RedeemedAuthorizationCodeGracePeriod { get; set; } = TimeSpan.FromSeconds(10);

    public IReadOnlyList<string> ScopesSupported { get; set; } =
    [
        OAuthScope.OpenId,
        OAuthScope.Profile,
        OAuthScope.Email,
        OAuthScope.OfflineAccess
    ];

    public IReadOnlyList<string> ClaimsSupported { get; set; } =
    [
        OAuthClaimTypes.Subject,
        OAuthClaimTypes.ClientId,
        OAuthClaimTypes.Name,
        OAuthClaimTypes.Email,
        OAuthClaimTypes.EmailVerified,
        OAuthClaimTypes.Groups,
        OAuthClaimTypes.Entitlements
    ];
}
