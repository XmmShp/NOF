using System.ComponentModel;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask CommandOutboundDelegate(CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask NotificationOutboundDelegate(CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask RequestOutboundDelegate(CancellationToken cancellationToken);

public interface ICommandOutboundMiddleware
{
    ValueTask InvokeAsync(CommandOutboundContext context, CommandOutboundDelegate next, CancellationToken cancellationToken);
}

public interface INotificationOutboundMiddleware
{
    ValueTask InvokeAsync(NotificationOutboundContext context, NotificationOutboundDelegate next, CancellationToken cancellationToken);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestOutboundMiddleware
{
    ValueTask InvokeAsync(RequestOutboundContext context, RequestOutboundDelegate next, CancellationToken cancellationToken);
}

public interface ICommandOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(CommandOutboundContext context, CommandOutboundDelegate dispatch, CancellationToken cancellationToken);
}

public interface INotificationOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(NotificationOutboundContext context, NotificationOutboundDelegate dispatch, CancellationToken cancellationToken);
}

public interface IRequestOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(RequestOutboundContext context, RequestOutboundDelegate dispatch, CancellationToken cancellationToken);
}

public abstract class RequestOutboundMiddleware : IRequestOutboundMiddleware
{
    public abstract ValueTask InvokeAsync(RequestOutboundContext context, RequestOutboundDelegate next, CancellationToken cancellationToken);
}
