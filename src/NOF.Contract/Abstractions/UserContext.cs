namespace NOF;

/// <summary>
/// Represents the current user context, providing access to identity, permissions, and custom properties.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets a value indicating whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the unique identifier of the current user, or <c>null</c> if not authenticated.
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// Gets the username of the current user, or <c>null</c> if not authenticated.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Gets the list of permissions assigned to the current user.
    /// </summary>
    IList<string> Permissions { get; }

    /// <summary>
    /// Gets a dictionary for storing custom user-related properties.
    /// Keys are typically strings or enums; values can be any object or <c>null</c>.
    /// </summary>
    IDictionary<object, object?> Properties { get; }

    /// <summary>
    /// Determines whether the current user has the specified permission.
    /// Supports exact match and wildcard patterns (e.g., "order.*").
    /// </summary>
    /// <param name="permission">The permission name to check.</param>
    /// <returns><c>true</c> if the user has the permission; otherwise, <c>false</c>.</returns>
    bool HasPermission(string permission);

    /// <summary>
    /// Sets the current user context synchronously.
    /// </summary>
    /// <param name="id">The user ID. Must not be null or empty.</param>
    /// <param name="username">The username. Must not be null or empty.</param>
    /// <param name="permissions">The list of permissions assigned to the user.</param>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="id"/> or <paramref name="username"/> is null or empty.</exception>
    void SetUser(string id, string username, IEnumerable<string> permissions);

    /// <summary>
    /// Sets the current user context asynchronously.
    /// </summary>
    /// <param name="id">The user ID. Must not be null or empty.</param>
    /// <param name="username">The username. Must not be null or empty.</param>
    /// <param name="permissions">The list of permissions assigned to the user.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="id"/> or <paramref name="username"/> is null or empty.</exception>
    Task SetUserAsync(string id, string username, IEnumerable<string> permissions);

    /// <summary>
    /// Clears the current user context, marking the user as unauthenticated.
    /// </summary>
    void UnsetUser();

    /// <summary>
    /// Clears the current user context asynchronously, marking the user as unauthenticated.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnsetUserAsync();
}
