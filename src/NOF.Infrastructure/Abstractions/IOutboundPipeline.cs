using NOF.Hosting;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask CommandOutboundHandlerDelegate(
    CommandOutboundContext context,
    object message,
    CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask NotificationOutboundHandlerDelegate(
    NotificationOutboundContext context,
    object message,
    CancellationToken cancellationToken);

public interface ICommandOutboundMiddleware
{
    ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken);
}

public interface INotificationOutboundMiddleware
{
    ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken);
}
