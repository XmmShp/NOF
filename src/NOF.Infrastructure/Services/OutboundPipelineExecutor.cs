using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandOutboundPipelineExecutor
{
    private readonly IReadOnlyList<ICommandOutboundMiddleware> _middlewares;

    public CommandOutboundPipelineExecutor(IEnumerable<ICommandOutboundMiddleware> middlewares)
    {
        _middlewares = new DependencyGraph<ICommandOutboundMiddleware>(middlewares).GetExecutionOrder();
    }

    public ValueTask ExecuteAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct) => middleware.InvokeAsync(currentContext, currentMessage, next, ct);
        }

        return pipeline(context, message, cancellationToken);
    }
}

public sealed class NotificationOutboundPipelineExecutor
{
    private readonly IReadOnlyList<INotificationOutboundMiddleware> _middlewares;

    public NotificationOutboundPipelineExecutor(IEnumerable<INotificationOutboundMiddleware> middlewares)
    {
        _middlewares = new DependencyGraph<INotificationOutboundMiddleware>(middlewares).GetExecutionOrder();
    }

    public ValueTask ExecuteAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct) => middleware.InvokeAsync(currentContext, currentMessage, next, ct);
        }

        return pipeline(context, message, cancellationToken);
    }
}
