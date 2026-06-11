using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandOutboundPipelineExecutor
{
    private readonly CommandOutboundPipelineTypes _middlewareTypes;
    private readonly IServiceProvider _services;

    public CommandOutboundPipelineExecutor(CommandOutboundPipelineTypes middlewareTypes, IServiceProvider services)
    {
        _middlewareTypes = middlewareTypes;
        _services = services;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (ICommandOutboundMiddleware)_services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct) => middleware.InvokeAsync(currentContext, currentMessage, next, ct);
        }

        return pipeline(context, message, cancellationToken);
    }
}

public sealed class NotificationOutboundPipelineExecutor
{
    private readonly NotificationOutboundPipelineTypes _middlewareTypes;
    private readonly IServiceProvider _services;

    public NotificationOutboundPipelineExecutor(NotificationOutboundPipelineTypes middlewareTypes, IServiceProvider services)
    {
        _middlewareTypes = middlewareTypes;
        _services = services;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (INotificationOutboundMiddleware)_services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct) => middleware.InvokeAsync(currentContext, currentMessage, next, ct);
        }

        return pipeline(context, message, cancellationToken);
    }
}
