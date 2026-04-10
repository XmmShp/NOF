using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class TenantOutboundMiddleware : IOutboundMiddleware
{
    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] =
            NOFAbstractionConstants.Tenant.NormalizeTenantId(context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId) ? tenantId : null);

        return next(cancellationToken);
    }
}
