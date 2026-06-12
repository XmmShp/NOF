using NOF.Abstraction;
using NOF.Hosting;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Applies explicitly supported ambient context values to outbound transport headers.
/// </summary>
public sealed class ContextHeadersOutboundMiddleware(ICurrentTenant currentTenant) :
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware,
    IRequestOutboundMiddleware
{
    public TopologyComparison Compare(ICommandOutboundMiddleware other)
        => other is MessageIdOutboundMiddleware ? TopologyComparison.Before : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationOutboundMiddleware other)
        => other is MessageIdOutboundMiddleware ? TopologyComparison.Before : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(IRequestOutboundMiddleware other)
        => other is MessageIdOutboundMiddleware ? TopologyComparison.Before : TopologyComparison.DoesNotMatter;

    public ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenantHeader(context);
        return next(context, message, cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenantHeader(context);
        return next(context, message, cancellationToken);
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        ApplyTenantHeader(context);
        return next(context, request, cancellationToken);
    }

    private void ApplyTenantHeader(Context context)
    {
        var tenantId = TenantId.Normalize(currentTenant.TenantId);
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            switch (context)
            {
                case CommandOutboundContext commandContext when !commandContext.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.TenantId):
                    commandContext.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] = tenantId;
                    break;
                case NotificationOutboundContext notificationContext when !notificationContext.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.TenantId):
                    notificationContext.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] = tenantId;
                    break;
                case RequestOutboundContext requestContext when !requestContext.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.TenantId):
                    requestContext.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] = tenantId;
                    break;
            }
        }
    }
}
