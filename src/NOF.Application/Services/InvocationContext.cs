using System.Security.Claims;

namespace NOF.Application;

/// <summary>
/// Default implementation of <see cref="IInvocationContext"/>.
/// </summary>
public class InvocationContext : IInvocationContextInternal
{
    /// <summary>
    /// A shared, unauthenticated <see cref="ClaimsPrincipal"/> instance.
    /// </summary>
    public static ClaimsPrincipal Anonymous { get; } = new();

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
        User = user;
    }

    /// <inheritdoc />
    public void UnsetUser()
    {
        User = Anonymous;
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

