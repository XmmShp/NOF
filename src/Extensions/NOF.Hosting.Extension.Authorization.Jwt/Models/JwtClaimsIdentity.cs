using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Hosting.Extension.Authorization.Jwt;

/// <summary>
/// A <see cref="ClaimsIdentity"/> that also carries the raw JWT token string.
/// </summary>
public sealed class JwtClaimsIdentity : ClaimsIdentity
{
    /// <summary>
    /// The raw JWT token string.
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Downstream propagation settings for this identity. Null means do not propagate.
    /// </summary>
    public JwtDownstreamPropagation? DownstreamPropagation { get; }

    public JwtClaimsIdentity(string token)
        : this(
            CreateIdentity(token),
            token,
            downstreamPropagation: new JwtDownstreamPropagation())
    {
    }

    public JwtClaimsIdentity(ClaimsIdentity identity, string token)
        : this(
            identity,
            token,
            downstreamPropagation: new JwtDownstreamPropagation())
    {
    }

    public JwtClaimsIdentity(
        ClaimsIdentity identity,
        string token,
        JwtDownstreamPropagation? downstreamPropagation)
        : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (downstreamPropagation is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(downstreamPropagation.HeaderName);
            ArgumentException.ThrowIfNullOrWhiteSpace(downstreamPropagation.TokenType);
        }

        Token = token;
        DownstreamPropagation = downstreamPropagation;
    }

    private static ClaimsIdentity CreateIdentity(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
    }
}
