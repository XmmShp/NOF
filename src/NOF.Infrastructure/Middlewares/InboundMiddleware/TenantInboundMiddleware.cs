using Microsoft.Extensions.Options;
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
    private readonly TenantOptions _tenantOptions;

    public TenantInboundMiddleware(IExecutionContext executionContext, IOptions<TenantOptions> tenantOptions)
    {
        _executionContext = executionContext;
        _tenantOptions = tenantOptions.Value;
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
        var tenantId = TenantId.Normalize(_tenantOptions.SingleTenantId);
        if (_tenantOptions.Mode != TenantMode.SingleTenant
            && _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = TenantId.Normalize(headerTenantId);
        }

        _executionContext.TenantId = tenantId;
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
    }
}
