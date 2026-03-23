using NOF.Contract;
using System.Security.Claims;

namespace NOF.Application;

/// <summary>
/// Default implementation of <see cref="IUserContext"/>.
/// </summary>
public class UserContext : IMutableUserContext
{
    /// <summary>
    /// A shared, unauthenticated <see cref="ClaimsPrincipal"/> instance.
    /// </summary>
    public static ClaimsPrincipal Anonymous { get; } = new();

    /// <inheritdoc />
    public event Action? UserStateChanging;

    /// <inheritdoc />
    public event Action? UserStateChanged;

    /// <inheritdoc />
    public ClaimsPrincipal User { get; private set; } = Anonymous;

    /// <inheritdoc />
    public void SetUser(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        UserStateChanging?.Invoke();
        User = user;
        UserStateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UnsetUser()
    {
        UserStateChanging?.Invoke();
        User = Anonymous;
        UserStateChanged?.Invoke();
    }
}
