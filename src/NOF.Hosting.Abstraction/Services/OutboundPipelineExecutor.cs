using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting;

public abstract class MessageOutboundPipelineExecutor<TContext, TMiddlewareContract> : IOutboundPipelineExecutor<TContext>
    where TContext : MessageOutboundContext
    where TMiddlewareContract : class, IMessageOutboundMiddleware<TContext>
{
    private readonly MessagePipelineTypes<TMiddlewareContract> _middlewareTypes;

    protected MessageOutboundPipelineExecutor(MessagePipelineTypes<TMiddlewareContract> middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(TContext context, OutboundDelegate<TContext> dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (TMiddlewareContract)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

public sealed class CommandOutboundPipelineExecutor : MessageOutboundPipelineExecutor<CommandOutboundContext, ICommandOutboundMiddleware>, ICommandOutboundPipelineExecutor
{
    public CommandOutboundPipelineExecutor(CommandOutboundPipelineTypes middlewareTypes) : base(middlewareTypes)
    {
    }
}

public sealed class NotificationOutboundPipelineExecutor : MessageOutboundPipelineExecutor<NotificationOutboundContext, INotificationOutboundMiddleware>, INotificationOutboundPipelineExecutor
{
    public NotificationOutboundPipelineExecutor(NotificationOutboundPipelineTypes middlewareTypes) : base(middlewareTypes)
    {
    }
}

public sealed class RequestOutboundPipelineExecutor : MessageOutboundPipelineExecutor<RequestOutboundContext, IRequestOutboundMiddleware>, IRequestOutboundPipelineExecutor
{
    public RequestOutboundPipelineExecutor(RequestOutboundPipelineTypes middlewareTypes) : base(middlewareTypes)
    {
    }
}
