using NOF.Contract;
using System.Security.Claims;

namespace NOF.Application;

/// <summary>
/// Default implementation of <see cref="IExecutionContext"/>.
/// </summary>
public class InvocationContext : IExecutionContext
{
    public static ClaimsPrincipal Anonymous { get; } = new();

    /// <inheritdoc />
    public event Action? StateChanging;

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <inheritdoc />
    public ClaimsPrincipal User { get; private set; } = Anonymous;

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

    /// <inheritdoc />
    public string? TraceId { get; private set; }

    /// <inheritdoc />
    public string? SpanId { get; private set; }

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

    /// <inheritdoc />
    public void SetTenantId(string? tenantId)
    {
        TenantId = tenantId;
    }

    /// <inheritdoc />
    public void SetTracingInfo(string? traceId, string? spanId)
    {
        TraceId = traceId;
        SpanId = spanId;
    }
}
