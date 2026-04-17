using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TenantInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware,
    IAfter<InboundExceptionMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public TenantInboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
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
        var tenantId = _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId)
            ? TenantId.Normalize(headerTenantId)
            : NOFAbstractionConstants.Tenant.HostId;

        _executionContext.TenantId = tenantId;
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
    }
}
