using NOF.Contract;
using System.Security.Claims;

namespace NOF.Infrastructure;

/// <summary>
/// Default infrastructure-level implementation of <see cref="IUserContext"/>.
/// </summary>
public sealed class UserContext : IMutableUserContext
{
    /// <summary>
    /// A shared, unauthenticated <see cref="ClaimsPrincipal"/> instance.
    /// </summary>
    public static ClaimsPrincipal Anonymous { get; } = new();

    /// <inheritdoc />
    public event Action? StateChanging;

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <inheritdoc />
    public ClaimsPrincipal User { get; private set; } = Anonymous;

    /// <inheritdoc />
    public void SetUser(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        StateChanging?.Invoke();
        User = user;
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UnsetUser()
    {
        StateChanging?.Invoke();
        User = Anonymous;
        StateChanged?.Invoke();
    }
}
