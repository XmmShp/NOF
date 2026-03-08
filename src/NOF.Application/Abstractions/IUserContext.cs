using System.Security.Claims;

namespace NOF.Application;

/// <summary>
/// Represents the current user information for the active logical execution context.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// The claims principal representing the current user.
    /// May be unauthenticated.
    /// </summary>
    ClaimsPrincipal User { get; }

    /// <summary>
    /// Occurs before the current user state changes.
    /// </summary>
    event Action? StateChanging;

    /// <summary>
    /// Occurs after the current user state has changed.
    /// </summary>
    event Action? StateChanged;

    /// <summary>
    /// Gets a value indicating whether the current user is authenticated.
    /// </summary>
    bool IsAuthenticated => User.IsAuthenticated;

    /// <summary>
    /// Gets the unique identifier of the current user.
    /// </summary>
    string? Id => User.Id;

    /// <summary>
    /// Gets the display name of the current user.
    /// </summary>
    string? Name => User.Name;

    /// <summary>
    /// Gets the permissions granted to the current user.
    /// </summary>
    IReadOnlyList<string> Permissions => User.Permissions;

    /// <summary>
    /// Determines whether the current user has the specified permission.
    /// </summary>
    /// <param name="permission">The permission to check.</param>
    /// <param name="comparison">The string comparison used for matching.</param>
    /// <returns><c>true</c> if the user has the permission; otherwise, <c>false</c>.</returns>
    bool HasPermission(string permission, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => User.HasPermission(permission, comparison);
}

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
