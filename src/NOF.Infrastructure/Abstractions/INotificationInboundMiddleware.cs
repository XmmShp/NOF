using NOF.Hosting;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask NotificationHandlerDelegate(
    NotificationInboundContext context,
    object message,
    CancellationToken cancellationToken);

public interface INotificationInboundMiddleware : ITopologizable<INotificationInboundMiddleware>
{
    ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken);
}
