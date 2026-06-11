using NOF.Abstraction;
using NOF.Hosting;
using System.Diagnostics;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

public sealed class TenantInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware,
    IAfter<InboundExceptionMiddleware>
{
    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenant(context);
        await next(cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenant(context);
        await next(cancellationToken);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenant(context);
        await next(cancellationToken);
    }

    private static void ApplyTenant(CommandInboundContext context)
        => context.Context = ApplyTenant(context.Context);

    private static void ApplyTenant(NotificationInboundContext context)
        => context.Context = ApplyTenant(context.Context);

    private static void ApplyTenant(RequestInboundContext context)
        => context.Context = ApplyTenant(context.Context);

    private static Context ApplyTenant(Context context)
    {
        var tenantId = context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId)
            ? TenantId.Normalize(headerTenantId)
            : NOFAbstractionConstants.Tenant.HostId;

        context = context.WithTenantId(tenantId);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
        return context;
    }
}
