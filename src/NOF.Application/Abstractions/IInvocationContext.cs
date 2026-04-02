using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Represents the context of a logical invocation (e.g., HTTP request, command execution,
/// event handling, background task). Provides ambient access to user, tenant, and metadata.
/// </summary>
public interface IInvocationContext
{
    /// <summary>
    /// Gets the current user context associated with this invocation.
    /// </summary>
    IUserContext UserContext { get; }

    /// <summary>
    /// The tenant ID under which this invocation is executing.
    /// Can be null for host-level operations, or a specific tenant ID for tenant operations.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Shared storage for components participating in this invocation.
    /// Similar to HttpContext.Items or AsyncLocal state.
    /// </summary>
    IDictionary<string, object?> Items { get; }

    /// <summary>
    /// The trace ID for distributed tracing across service boundaries.
    /// </summary>
    string? TraceId { get; }

    /// <summary>
    /// The span ID for the current operation within the trace.
    /// </summary>
    string? SpanId { get; }

    void SetTenantId(string? tenantId);

    void SetTracingInfo(string? traceId, string? spanId);
}
