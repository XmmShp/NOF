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

    /// <summary>
    /// Creates a JWT claims principal from an existing principal and the raw token.
    /// </summary>
    public JwtClaimsPrincipal(ClaimsPrincipal principal, string token)
        : base(principal)
    {
        Token = token;
    }
}
