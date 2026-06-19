using System.ComponentModel.DataAnnotations;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OAuthAuthorizationServerOptions
{
    [Required(ErrorMessage = "Issuer is required.")]
    public string Issuer { get; set; } = string.Empty;

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
        OAuthScope.Email
    ];

    public IReadOnlyList<string> ClaimsSupported { get; set; } =
    [
        OAuthClaimTypes.Subject,
        OAuthClaimTypes.Name,
        OAuthClaimTypes.Email,
        OAuthClaimTypes.Groups
    ];
}
