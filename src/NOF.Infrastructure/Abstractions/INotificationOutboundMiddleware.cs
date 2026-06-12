using NOF.Hosting;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask NotificationOutboundHandlerDelegate(
    NotificationOutboundContext context,
    object message,
    CancellationToken cancellationToken);

public interface INotificationOutboundMiddleware : ITopologizable<INotificationOutboundMiddleware>
{
    ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken);
}
