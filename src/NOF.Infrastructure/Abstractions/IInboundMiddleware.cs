using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask CommandHandlerDelegate(
    CommandInboundContext context,
    object message,
    CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask NotificationHandlerDelegate(
    NotificationInboundContext context,
    object message,
    CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask RequestHandlerDelegate(
    RequestInboundContext context,
    object request,
    CancellationToken cancellationToken);

public interface ICommandInboundMiddleware
{
    ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken);
}

public interface INotificationInboundMiddleware
{
    ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestInboundMiddleware
{
    ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken);
}
