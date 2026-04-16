using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class CommandTenantOutboundMiddleware : ICommandOutboundMiddleware
{
    public ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] =
            NOFAbstractionConstants.Tenant.NormalizeTenantId(context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId) ? tenantId : null);

        return next(cancellationToken);
    }
}

public sealed class NotificationTenantOutboundMiddleware : INotificationOutboundMiddleware
{
    public ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] =
            NOFAbstractionConstants.Tenant.NormalizeTenantId(context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId) ? tenantId : null);

        return next(cancellationToken);
    }
}

public sealed class RequestTenantOutboundMiddleware : IRequestOutboundMiddleware
{
    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] =
            NOFAbstractionConstants.Tenant.NormalizeTenantId(context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId) ? tenantId : null);

        return next(cancellationToken);
    }
}
