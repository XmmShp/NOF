using NOF.Abstraction;
using NOF.Hosting;
using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>
/// Copies the current <see cref="Context"/> headers into outbound headers
/// so outbound operations can propagate tenant/tracing/auth without mutating the ambient execution context.
/// </summary>
public sealed class ContextHeadersOutboundMiddleware :
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
        context.CopyHeadersTo(context.Headers);
        return next(context, message, cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        context.CopyHeadersTo(context.Headers);
        return next(context, message, cancellationToken);
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        context.CopyHeadersTo(context.Headers);
        return next(context, request, cancellationToken);
    }
}
