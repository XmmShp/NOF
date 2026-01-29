using System.Security.Claims;

namespace NOF;

/// <summary>
/// Represents the context of a logical invocation (e.g., HTTP request, command execution,
/// event handling, background task). Provides ambient access to user, tenant, and metadata.
/// </summary>
public interface IInvocationContext
{
    /// <summary>
    /// The authenticated user principal associated with this invocation.
    /// May be unauthenticated (e.g., system-triggered events).
    /// </summary>
    ClaimsPrincipal User { get; }

    /// <summary>
    /// The tenant ID under which this invocation is executing. Never null.
    /// Defaults to "default" if not explicitly specified.
    /// </summary>
    string TenantId { get; }

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
    /// Sets the current user context asynchronously.
    /// </summary>
    /// <param name="user">The claims principal representing the authenticated user.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetUserAsync(ClaimsPrincipal user);

    /// <summary>
    /// Clears the current user context, marking the user as unauthenticated.
    /// </summary>
    void UnsetUser();

    /// <summary>
    /// Clears the current user context asynchronously, marking the user as unauthenticated.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnsetUserAsync();

    /// <summary>
    /// Sets the current tenant identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier. Must not be null or empty.</param>
    void SetTenantId(string tenantId);

    /// <summary>
    /// Sets the tracing information for this invocation.
    /// </summary>
    /// <param name="traceId">The trace ID for distributed tracing.</param>
    /// <param name="spanId">The span ID for the current operation.</param>
    void SetTracingInfo(string? traceId, string? spanId);
}

/// <summary>
/// Default implementation of <see cref="IInvocationContext"/>.
/// </summary>
public class InvocationContext : IInvocationContextInternal
{
    /// <inheritdoc />
    public ClaimsPrincipal User { get; private set; } = new();

    /// <inheritdoc />
    public string TenantId { get; private set; } = "default";

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
    public Task SetUserAsync(ClaimsPrincipal user)
        => Task.Run(() => SetUser(user));

    /// <inheritdoc />
    public void UnsetUser()
    {
        User = new ClaimsPrincipal();
    }

    /// <inheritdoc />
    public Task UnsetUserAsync()
        => Task.Run(UnsetUser);

    /// <inheritdoc />
    public void SetTenantId(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException("Tenant ID cannot be null or empty.");

        TenantId = tenantId;
    }

    /// <inheritdoc />
    public void SetTracingInfo(string? traceId, string? spanId)
    {
        TraceId = traceId;
        SpanId = spanId;
    }
}

