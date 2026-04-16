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

public interface ICommandOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(CommandOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken);
}

public interface INotificationOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(NotificationOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken);
}
