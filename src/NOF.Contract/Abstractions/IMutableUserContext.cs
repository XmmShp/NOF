using System.Security.Claims;

namespace NOF.Contract;

/// <summary>
/// Represents mutable operations for the current user context.
/// </summary>
public interface IMutableUserContext : IUserContext
{
    /// <summary>
    /// Sets the current user context.
    /// </summary>
    /// <param name="user">The claims principal representing the authenticated user.</param>
    void SetUser(ClaimsPrincipal user);

    /// <summary>
    /// Clears the current user context, marking the user as unauthenticated.
    /// </summary>
    void UnsetUser();
}
