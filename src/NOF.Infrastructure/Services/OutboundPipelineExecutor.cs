using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandOutboundPipelineExecutor : ICommandOutboundPipelineExecutor
{
    private readonly CommandOutboundPipelineTypes _middlewareTypes;

    public CommandOutboundPipelineExecutor(CommandOutboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(CommandOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (ICommandOutboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

public sealed class NotificationOutboundPipelineExecutor : INotificationOutboundPipelineExecutor
{
    private readonly NotificationOutboundPipelineTypes _middlewareTypes;

    public NotificationOutboundPipelineExecutor(NotificationOutboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(NotificationOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (INotificationOutboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}
