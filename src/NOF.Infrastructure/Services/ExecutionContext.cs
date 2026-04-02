using NOF.Application;
using System.Security.Claims;

namespace NOF.Infrastructure;

public sealed class ExecutionContext : IExecutionContext
{
    public static ClaimsPrincipal Anonymous { get; } = new();

    public event Action? StateChanging;
    public event Action? StateChanged;

    public ClaimsPrincipal User { get; private set; } = Anonymous;

    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public void SetUser(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        StateChanging?.Invoke();
        User = user;
        StateChanged?.Invoke();
    }

    public void UnsetUser()
    {
        StateChanging?.Invoke();
        User = Anonymous;
        StateChanged?.Invoke();
    }
}
