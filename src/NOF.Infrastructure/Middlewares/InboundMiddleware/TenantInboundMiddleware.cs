using NOF.Abstraction;
using NOF.Hosting;
using System.Diagnostics;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class TenantInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware,
    IAfter<InboundExceptionMiddleware>
{
    private readonly NOFContext _contextAccessor;

    public TenantInboundMiddleware(NOFContext contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenant();
        await next(cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenant();
        await next(cancellationToken);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenant();
        await next(cancellationToken);
    }

    private void ApplyTenant()
    {
        var context = _contextAccessor;
        var tenantId = context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId)
            ? TenantId.Normalize(headerTenantId)
            : NOFAbstractionConstants.Tenant.HostId;

        context.TenantId = tenantId;
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
    }
}
