using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandInboundPipelineExecutor : ICommandInboundPipelineExecutor
{
    private readonly CommandInboundPipelineTypes _middlewareTypes;

    public CommandInboundPipelineExecutor(CommandInboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(CommandInboundContext context, HandlerDelegate inbound, CancellationToken cancellationToken)
    {
        var pipeline = inbound;
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (ICommandInboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

public sealed class NotificationInboundPipelineExecutor : INotificationInboundPipelineExecutor
{
    private readonly NotificationInboundPipelineTypes _middlewareTypes;

    public NotificationInboundPipelineExecutor(NotificationInboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(NotificationInboundContext context, HandlerDelegate inbound, CancellationToken cancellationToken)
    {
        var pipeline = inbound;
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (INotificationInboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

public sealed class RequestInboundPipelineExecutor : IRequestInboundPipelineExecutor
{
    private readonly RequestInboundPipelineTypes _middlewareTypes;

    public RequestInboundPipelineExecutor(RequestInboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(RequestInboundContext context, HandlerDelegate inbound, CancellationToken cancellationToken)
    {
        var pipeline = inbound;
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IRequestInboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}
