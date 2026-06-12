using System.ComponentModel.DataAnnotations;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OAuthAuthorizationServerOptions
{
    [Required]
    public string Issuer { get; set; } = string.Empty;

    public string PathBase { get; set; } = "/oauth2";

    public string AccessTokenAudience { get; set; } = "nof-app";

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
