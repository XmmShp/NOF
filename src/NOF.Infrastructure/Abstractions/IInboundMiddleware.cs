using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask InboundDelegate<TContext>(CancellationToken cancellationToken);

public interface IMessageInboundMiddleware<TContext>
    where TContext : MessageInboundContext
{
    ValueTask InvokeAsync(TContext context, InboundDelegate<TContext> next, CancellationToken cancellationToken);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICommandInboundMiddleware : IMessageInboundMiddleware<CommandInboundContext>;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface INotificationInboundMiddleware : IMessageInboundMiddleware<NotificationInboundContext>;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestInboundMiddleware : IMessageInboundMiddleware<RequestInboundContext>;

public abstract class AllMessagesInboundMiddleware : ICommandInboundMiddleware, INotificationInboundMiddleware, IRequestInboundMiddleware
{
    public ValueTask InvokeAsync(CommandInboundContext context, InboundDelegate<CommandInboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    public ValueTask InvokeAsync(NotificationInboundContext context, InboundDelegate<NotificationInboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    public ValueTask InvokeAsync(RequestInboundContext context, InboundDelegate<RequestInboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    protected abstract ValueTask InvokeAsyncCore(MessageInboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken);
}

public abstract class CommandAndNotificationInboundMiddleware : ICommandInboundMiddleware, INotificationInboundMiddleware
{
    public ValueTask InvokeAsync(CommandInboundContext context, InboundDelegate<CommandInboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    public ValueTask InvokeAsync(NotificationInboundContext context, InboundDelegate<NotificationInboundContext> next, CancellationToken cancellationToken)
        => InvokeAsyncCore(context, ct => next(ct), cancellationToken);

    protected abstract ValueTask InvokeAsyncCore(MessageInboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken);
}

public abstract class RequestInboundMiddleware : IRequestInboundMiddleware
{
    public abstract ValueTask InvokeAsync(RequestInboundContext context, InboundDelegate<RequestInboundContext> next, CancellationToken cancellationToken);
}
