using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Hosting.Extension.Authorization.Jwt;

/// <summary>
/// A <see cref="ClaimsPrincipal"/> that also carries the raw JWT token string.
/// </summary>
public sealed class JwtClaimsPrincipal : ClaimsPrincipal
{
    /// <summary>
    /// The raw JWT token string.
    /// </summary>
    public string Token { get; }

    private JwtClaimsPrincipal(ClaimsIdentity identity, string token)
        : base(identity)
    {
        Token = token;
    }

    public static JwtClaimsPrincipal FromToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
        return new JwtClaimsPrincipal(identity, token);
    }
}
