using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>Propagates tenant context to outbound messages.</summary>
public class TenantOutboundMiddlewareStep : IOutboundMiddlewareStep<TenantOutboundMiddlewareStep, TenantOutboundMiddleware>,
    IAfter<TracingOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that propagates the current <see cref="IInvocationContext.TenantId"/>
/// into the <see cref="NOFInfrastructureConstants.Transport.Headers.TenantId"/> header.
/// </summary>
public sealed class TenantOutboundMiddleware : IOutboundMiddleware
{
    private readonly IInvocationContext _invocationContext;

    public TenantOutboundMiddleware(IInvocationContext invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_invocationContext.TenantId))
        {
            context.Headers.TryAdd(NOFInfrastructureConstants.Transport.Headers.TenantId, _invocationContext.TenantId);
        }

        return next(cancellationToken);
    }
}
