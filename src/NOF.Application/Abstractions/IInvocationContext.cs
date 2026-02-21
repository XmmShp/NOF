using System.Security.Claims;

namespace NOF.Application;

/// <summary>
/// Represents the context of a logical invocation (e.g., HTTP request, command execution,
/// event handling, background task). Provides ambient access to user, tenant, and metadata.
/// </summary>
public interface IInvocationContext
{
    /// <summary>
    /// The claims principal representing the current user.
    /// May be unauthenticated (e.g., system-triggered events).
    /// </summary>
    ClaimsPrincipal User { get; }

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
}

/// <summary>
/// Internal interface for mutable invocation context operations.
/// </summary>
public interface IInvocationContextInternal : IInvocationContext
{
    /// <summary>
    /// Sets the current user context.
    /// </summary>
    /// <param name="user">The claims principal representing the authenticated user.</param>
    void SetUser(ClaimsPrincipal user);

    /// <summary>
    /// Clears the current user context, marking the user as unauthenticated.
    /// </summary>
    void UnsetUser();

    /// <summary>
    /// Sets the current tenant identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier. Can be null for host-level operations.</param>
    void SetTenantId(string? tenantId);

    /// <summary>
    /// Sets the tracing information for this invocation.
    /// </summary>
    /// <param name="traceId">The trace ID for distributed tracing.</param>
    /// <param name="spanId">The span ID for the current operation.</param>
    void SetTracingInfo(string? traceId, string? spanId);
}
