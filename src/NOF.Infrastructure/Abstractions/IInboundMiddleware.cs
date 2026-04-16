using NOF.Hosting;
using System.ComponentModel;

namespace NOF.Infrastructure;

public interface ICommandInboundMiddleware
{
    ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public interface INotificationInboundMiddleware
{
    ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestInboundMiddleware
{
    ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public abstract class RequestInboundMiddleware : IRequestInboundMiddleware
{
    public abstract ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}
