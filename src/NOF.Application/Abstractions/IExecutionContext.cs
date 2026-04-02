using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Represents the context of a logical execution (e.g., HTTP request, command execution,
/// event handling, background task). Provides ambient access to user, tenant, and metadata.
/// </summary>
public interface IExecutionContext : IUserContext
{
    string TenantId { get; }

    /// <summary>
    /// Shared storage for components participating in this execution.
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

    void SetTenantId(string tenantId);

    void SetTracingInfo(string? traceId, string? spanId);
}
