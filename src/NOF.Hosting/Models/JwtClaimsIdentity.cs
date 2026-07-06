using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Hosting;

/// <summary>
/// A <see cref="ClaimsIdentity"/> that also carries the raw access token string.
/// </summary>
public sealed class JwtClaimsIdentity : ClaimsIdentity
{
    /// <summary>
    /// The raw access token string.
    /// </summary>
    public string Token { get; }

    public JwtClaimsIdentity(string token)
        : this(
            CreateIdentity(token),
            token)
    {
    }

    public JwtClaimsIdentity(ClaimsIdentity identity, string token)
        : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        Token = token;
    }

    private static ClaimsIdentity CreateIdentity(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new ClaimsIdentity(
            jwt.Claims,
            authenticationType: "jwt",
            nameType: "name",
            roleType: ClaimTypes.Role);
    }
}
