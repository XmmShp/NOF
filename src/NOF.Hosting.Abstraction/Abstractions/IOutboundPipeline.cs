using System.ComponentModel;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask OutboundDelegate<TContext>(CancellationToken cancellationToken);

public interface IMessageOutboundMiddleware<TContext>
    where TContext : MessageOutboundContext
{
    ValueTask InvokeAsync(TContext context, OutboundDelegate<TContext> next, CancellationToken cancellationToken);
}

public interface IOutboundPipelineExecutor<TContext>
    where TContext : MessageOutboundContext
{
    ValueTask ExecuteAsync(TContext context, OutboundDelegate<TContext> dispatch, CancellationToken cancellationToken);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICommandOutboundMiddleware : IMessageOutboundMiddleware<CommandOutboundContext>;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface INotificationOutboundMiddleware : IMessageOutboundMiddleware<NotificationOutboundContext>;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestOutboundMiddleware : IMessageOutboundMiddleware<RequestOutboundContext>;

public interface ICommandOutboundPipelineExecutor : IOutboundPipelineExecutor<CommandOutboundContext>;

public interface INotificationOutboundPipelineExecutor : IOutboundPipelineExecutor<NotificationOutboundContext>;

public interface IRequestOutboundPipelineExecutor : IOutboundPipelineExecutor<RequestOutboundContext>;

public abstract class AllMessagesOutboundMiddleware : ICommandOutboundMiddleware, INotificationOutboundMiddleware, IRequestOutboundMiddleware
{
    public ValueTask InvokeAsync(CommandOutboundContext context, OutboundDelegate<CommandOutboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    public ValueTask InvokeAsync(NotificationOutboundContext context, OutboundDelegate<NotificationOutboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    public ValueTask InvokeAsync(RequestOutboundContext context, OutboundDelegate<RequestOutboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    protected abstract ValueTask InvokeAsyncCore(MessageOutboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken);
}

public abstract class RequestOutboundMiddleware : IRequestOutboundMiddleware
{
    public abstract ValueTask InvokeAsync(RequestOutboundContext context, OutboundDelegate<RequestOutboundContext> next, CancellationToken cancellationToken);
}
