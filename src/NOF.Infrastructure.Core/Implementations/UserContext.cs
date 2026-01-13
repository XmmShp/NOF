namespace NOF;

/// <summary>
/// Default implementation of <see cref="IUserContext"/>.
/// </summary>
public class UserContext : IUserContext
{
    private readonly StringComparison _comparison;

    public UserContext(StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        _comparison = comparison;
    }

    /// <inheritdoc />
    public bool IsAuthenticated => Id is not null;

    /// <inheritdoc />
    public string? Id { get; private set; }

    /// <inheritdoc />
    public string? Username { get; private set; }

    private readonly List<string> _permissions = [];

    /// <inheritdoc />
    public IList<string> Permissions => _permissions;

    /// <inheritdoc />
    public IDictionary<object, object?> Properties { get; } = new Dictionary<object, object?>();

    /// <inheritdoc />
    public bool HasPermission(string permission)
    {
        if (string.IsNullOrEmpty(permission))
            return false;

        var comparer = StringComparer.FromComparison(_comparison);

        // Exact match
        if (Permissions.Contains(permission, comparer))
            return true;

        // Wildcard match (e.g., "order.*", "admin.*.delete")
        return Permissions
            .Where(p => p?.Contains('*') == true)
            .Any(pattern => permission.MatchWildcard(pattern!, _comparison));
    }

    /// <inheritdoc />
    public void SetUser(string id, string username, IEnumerable<string> permissions)
    {
        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException("User ID cannot be null or empty.");

        if (string.IsNullOrEmpty(username))
            throw new InvalidOperationException("Username cannot be null or empty.");

        Id = id;
        Username = username;
        Permissions.Clear();
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
        Permissions.Clear();
        Properties.Clear();
    }

    /// <inheritdoc />
    public Task UnsetUserAsync()
        => Task.Run(UnsetUser);
}