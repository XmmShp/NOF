namespace NOF.Hosting;

public sealed class TenantOutboundMiddleware : IOutboundMiddleware
{
    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        context.Headers[NOFHostingConstants.Transport.Headers.TenantId] =
            NOFHostingConstants.Tenant.NormalizeTenantId(context.Headers.TryGetValue(NOFHostingConstants.Transport.Headers.TenantId, out var tenantId) ? tenantId : null);

        return next(cancellationToken);
    }
}
