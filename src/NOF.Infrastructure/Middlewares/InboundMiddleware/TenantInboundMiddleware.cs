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
    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = ApplyTenant(context);
        await next(executionContext, message, cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = ApplyTenant(context);
        await next(executionContext, message, cancellationToken);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = ApplyTenant(context);
        await next(executionContext, request, cancellationToken);
    }

    private static TContext ApplyTenant<TContext>(TContext context)
        where TContext : Context
    {
        var tenantId = context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId)
            ? TenantId.Normalize(headerTenantId)
            : NOFAbstractionConstants.Tenant.HostId;

        context = (TContext)context.WithTenantId(tenantId);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
        return context;
    }
}
