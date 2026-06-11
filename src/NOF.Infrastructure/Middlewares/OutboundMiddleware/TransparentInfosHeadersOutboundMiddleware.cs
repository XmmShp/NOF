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
    IRequestOutboundMiddleware,
    IBefore<MessageIdOutboundMiddleware>
{
    public ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        context.Context.CopyHeadersTo(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        context.Context.CopyHeadersTo(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        context.Context.CopyHeadersTo(context.Headers);
        return next(cancellationToken);
    }
}
