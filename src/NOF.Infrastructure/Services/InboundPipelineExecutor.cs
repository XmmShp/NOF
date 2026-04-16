using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure;

public abstract class MessageInboundPipelineExecutor<TContext, TMiddlewareContract> : IInboundPipelineExecutor<TContext>
    where TContext : MessageInboundContext
    where TMiddlewareContract : class, IMessageInboundMiddleware<TContext>
{
    private readonly MessagePipelineTypes<TMiddlewareContract> _middlewareTypes;

    protected MessageInboundPipelineExecutor(MessagePipelineTypes<TMiddlewareContract> middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(TContext context, InboundDelegate<TContext> inbound, CancellationToken cancellationToken)
    {
        var pipeline = inbound;
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (TMiddlewareContract)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

public sealed class CommandInboundPipelineExecutor : MessageInboundPipelineExecutor<CommandInboundContext, ICommandInboundMiddleware>, ICommandInboundPipelineExecutor
{
    public CommandInboundPipelineExecutor(CommandInboundPipelineTypes middlewareTypes) : base(middlewareTypes)
    {
    }
}

public sealed class NotificationInboundPipelineExecutor : MessageInboundPipelineExecutor<NotificationInboundContext, INotificationInboundMiddleware>, INotificationInboundPipelineExecutor
{
    public NotificationInboundPipelineExecutor(NotificationInboundPipelineTypes middlewareTypes) : base(middlewareTypes)
    {
    }
}

public sealed class RequestInboundPipelineExecutor : MessageInboundPipelineExecutor<RequestInboundContext, IRequestInboundMiddleware>, IRequestInboundPipelineExecutor
{
    public RequestInboundPipelineExecutor(RequestInboundPipelineTypes middlewareTypes) : base(middlewareTypes)
    {
    }
}
