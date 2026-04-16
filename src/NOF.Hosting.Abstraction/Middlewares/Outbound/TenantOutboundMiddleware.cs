using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class TenantOutboundMiddleware : ICommandOutboundMiddleware, INotificationOutboundMiddleware, IRequestOutboundMiddleware
{
    public ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        NormalizeTenantHeader(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        NormalizeTenantHeader(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        NormalizeTenantHeader(context.Headers);
        return next(cancellationToken);
    }

    private static void NormalizeTenantHeader(IDictionary<string, string?> headers)
    {
        headers[NOFAbstractionConstants.Transport.Headers.TenantId] =
            NOFAbstractionConstants.Tenant.NormalizeTenantId(
                headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId) ? tenantId : null);
    }
}
