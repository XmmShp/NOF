using NOF.Hosting;

namespace NOF.Infrastructure;

public interface ICommandOutboundMiddleware
{
    ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public interface INotificationOutboundMiddleware
{
    ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}
