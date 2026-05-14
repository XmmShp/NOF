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

    public JwtClaimsPrincipal(string token)
        : this(CreateIdentity(token), token)
    {
    }

    public JwtClaimsPrincipal(ClaimsIdentity identity, string token)
        : base(identity)
    {
        Token = token;
    }

    private static ClaimsIdentity CreateIdentity(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
    }
}
