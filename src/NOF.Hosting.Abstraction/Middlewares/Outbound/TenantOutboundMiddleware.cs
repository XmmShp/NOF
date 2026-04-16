using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class TenantOutboundMiddleware : AllMessagesOutboundMiddleware
{
    protected override ValueTask InvokeAsyncCore(MessageOutboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] =
            NOFAbstractionConstants.Tenant.NormalizeTenantId(context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId) ? tenantId : null);

        return next(cancellationToken);
    }
}
