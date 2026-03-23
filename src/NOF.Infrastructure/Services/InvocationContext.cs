using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Default implementation of <see cref="IInvocationContext"/>.
/// </summary>
public class InvocationContext : IMutableInvocationContext
{
    public InvocationContext(IMutableUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        UserContext = userContext;
    }

    /// <inheritdoc />
    public IMutableUserContext UserContext { get; }

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

    /// <inheritdoc />
    public string? TraceId { get; private set; }

    /// <inheritdoc />
    public string? SpanId { get; private set; }

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
