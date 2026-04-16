using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class CommandTenantInboundMiddleware : ICommandInboundMiddleware, IAfter<CommandExceptionInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;
    private readonly TenantOptions _tenantOptions;

    public CommandTenantInboundMiddleware(IExecutionContext executionContext, IOptions<TenantOptions> tenantOptions)
    {
        _executionContext = executionContext;
        _tenantOptions = tenantOptions.Value;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(_tenantOptions.SingleTenantId);
        if (_tenantOptions.Mode != TenantMode.SingleTenant
            && _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(headerTenantId);
        }

        _executionContext.TenantId = tenantId;
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);

        await next(cancellationToken);
    }
}

public sealed class NotificationTenantInboundMiddleware : INotificationInboundMiddleware, IAfter<NotificationExceptionInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;
    private readonly TenantOptions _tenantOptions;

    public NotificationTenantInboundMiddleware(IExecutionContext executionContext, IOptions<TenantOptions> tenantOptions)
    {
        _executionContext = executionContext;
        _tenantOptions = tenantOptions.Value;
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(_tenantOptions.SingleTenantId);
        if (_tenantOptions.Mode != TenantMode.SingleTenant
            && _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(headerTenantId);
        }

        _executionContext.TenantId = tenantId;
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);

        await next(cancellationToken);
    }
}

public sealed class RequestTenantInboundMiddleware : IRequestInboundMiddleware, IAfter<RequestExceptionInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;
    private readonly TenantOptions _tenantOptions;

    public RequestTenantInboundMiddleware(IExecutionContext executionContext, IOptions<TenantOptions> tenantOptions)
    {
        _executionContext = executionContext;
        _tenantOptions = tenantOptions.Value;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(_tenantOptions.SingleTenantId);
        if (_tenantOptions.Mode != TenantMode.SingleTenant
            && _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(headerTenantId);
        }

        _executionContext.TenantId = tenantId;
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);

        await next(cancellationToken);
    }
}
