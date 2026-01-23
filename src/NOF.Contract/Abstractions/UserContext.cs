using System.Diagnostics.CodeAnalysis;

namespace NOF;

/// <summary>
/// Represents the current user context, providing access to identity, permissions, and custom properties.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets a value indicating whether the user is authenticated.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Id), nameof(Username))]
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
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// Gets a dictionary for storing custom user-related properties.
    /// Keys are strings; values can be any object or <c>null</c>.
    /// </summary>
    IDictionary<string, object?> Properties { get; }
}

public interface IUserContextInternal : IUserContext
{
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

/// <summary>
/// Default implementation of <see cref="IUserContext"/>.
/// </summary>
public class UserContext : IUserContextInternal
{
    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(Id), nameof(Username))]
    public bool IsAuthenticated => Id is not null;

    /// <inheritdoc />
    public string? Id { get; private set; }

    /// <inheritdoc />
    public string? Username { get; private set; }

    private readonly List<string> _permissions = [];

    /// <inheritdoc />
    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    /// <inheritdoc />
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

    /// <inheritdoc />
    public void SetUser(string id, string username, IEnumerable<string> permissions)
    {
        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException("User ID cannot be null or empty.");

        if (string.IsNullOrEmpty(username))
            throw new InvalidOperationException("Username cannot be null or empty.");

        Id = id;
        Username = username;
        _permissions.Clear();
        _permissions.AddRange(permissions);
    }

    /// <inheritdoc />
    public Task SetUserAsync(string id, string username, IEnumerable<string> permissions)
        => Task.Run(() => SetUser(id, username, permissions));

    /// <inheritdoc />
    public void UnsetUser()
    {
        Id = null;
        Username = null;
        _permissions.Clear();
        Properties.Clear();
    }

    /// <inheritdoc />
    public Task UnsetUserAsync()
        => Task.Run(UnsetUser);
}

public static partial class __NOF_Contract_Extensions__
{
    extension(IUserContext context)
    {
        /// <summary>
        /// Determines whether the current user has the specified permission.
        /// Supports exact match and wildcard patterns (e.g., "order.*").
        /// </summary>
        /// <param name="permission">The permission name to check.</param>
        /// <param name="comparison"></param>
        /// <returns><c>true</c> if the user has the permission; otherwise, <c>false</c>.</returns>
        public bool HasPermission(string permission, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(permission))
                return false;

            var comparer = StringComparer.FromComparison(comparison);

            // Exact match
            if (context.Permissions.Contains(permission, comparer))
                return true;

            // Wildcard match (e.g., "order.*", "admin.*.delete")
            return context.Permissions
                .Where(p => p?.Contains('*') == true)
                .Any(pattern => permission.MatchWildcard(pattern, comparison));
        }
    }
}