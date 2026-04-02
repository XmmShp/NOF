using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>Propagates tenant context to outbound messages.</summary>
public class TenantOutboundMiddlewareStep : IOutboundMiddlewareStep<TenantOutboundMiddlewareStep, TenantOutboundMiddleware>,
    IAfter<TracingOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that propagates the current <see cref="IExecutionContext.TenantId"/>
/// into the <see cref="NOFInfrastructureConstants.Transport.Headers.TenantId"/> header.
/// </summary>
public sealed class TenantOutboundMiddleware : IOutboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public TenantOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        context.ExecutionContext.Headers[NOFApplicationConstants.Transport.Headers.TenantId] =
            NOFApplicationConstants.Tenant.NormalizeTenantId(_executionContext.TenantId);

        return next(cancellationToken);
    }
}
