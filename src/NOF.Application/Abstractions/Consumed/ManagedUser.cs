using System.Security.Claims;

namespace NOF.Application;

/// <summary>
/// Represents a managed user identity that wraps a <see cref="ClaimsPrincipal"/>
/// along with the raw JWT token string for downstream propagation.
/// </summary>
public sealed class ManagedUser
{
    /// <summary>
    /// An empty, unauthenticated user.
    /// </summary>
    public static ManagedUser Anonymous { get; } = new();

    /// <summary>
    /// The claims principal representing the authenticated user.
    /// </summary>
    public ClaimsPrincipal Principal { get; }

    /// <summary>
    /// The raw JWT token string (e.g., from the Authorization header).
    /// Can be used to propagate the token to downstream services.
    /// </summary>
    public string? Token { get; }

    /// <summary>
    /// Creates an anonymous (unauthenticated) managed user.
    /// </summary>
    public ManagedUser()
    {
        Principal = new ClaimsPrincipal();
        Token = null;
    }

    /// <summary>
    /// Creates a managed user from a claims principal and optional raw token.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <param name="token">The raw JWT token string.</param>
    public ManagedUser(ClaimsPrincipal principal, string? token = null)
    {
        Principal = principal;
        Token = token;
    }
}
