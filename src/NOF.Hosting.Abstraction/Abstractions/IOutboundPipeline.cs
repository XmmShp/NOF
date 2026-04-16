using System.ComponentModel;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask HandlerDelegate(CancellationToken cancellationToken);

public interface ICommandOutboundMiddleware
{
    ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public interface INotificationOutboundMiddleware
{
    ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestOutboundMiddleware
{
    ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public interface ICommandOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(CommandOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken);
}

public interface INotificationOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(NotificationOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken);
}

public interface IRequestOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(RequestOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken);
}

public abstract class RequestOutboundMiddleware : IRequestOutboundMiddleware
{
    public abstract ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}
