using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TenantInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware
{
    public TopologyComparison Compare(ICommandInboundMiddleware other)
        => other is TracingInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationInboundMiddleware other)
        => other is TracingInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other is TracingInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(context);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
        await next((CommandInboundContext)context.WithTenantId(tenantId), message, cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(context);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
        await next((NotificationInboundContext)context.WithTenantId(tenantId), message, cancellationToken);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(context);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
        await next((RequestInboundContext)context.WithTenantId(tenantId), request, cancellationToken);
    }

    private static string GetTenantId(Context context)
    {
        return context.TryGetItem(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId)
            && headerTenantId is string tenantIdValue
            ? TenantId.Normalize(tenantIdValue)
            : TenantId.Normalize(null);
    }
}
