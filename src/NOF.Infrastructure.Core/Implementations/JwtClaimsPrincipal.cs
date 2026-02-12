using System.Security.Claims;

namespace NOF.Infrastructure.Core;

/// <summary>
/// A <see cref="ClaimsPrincipal"/> that also carries the raw JWT token string.
/// Used by <see cref="JwtIdentityResolver"/> so that downstream middleware
/// (e.g., <see cref="JwtAuthorizationOutboundMiddleware"/>) can propagate the token
/// via pattern matching: <c>if (user is JwtClaimsPrincipal jwt) { ... jwt.Token ... }</c>.
/// </summary>
public sealed class JwtClaimsPrincipal : ClaimsPrincipal
{
    /// <summary>
    /// The raw JWT token string (e.g., from the Authorization header).
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Creates a JWT claims principal from an existing principal and the raw token.
    /// </summary>
    /// <param name="principal">The validated claims principal.</param>
    /// <param name="token">The raw JWT token string.</param>
    public JwtClaimsPrincipal(ClaimsPrincipal principal, string token)
        : base(principal)
    {
        Token = token;
    }
}
