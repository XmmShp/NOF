using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting;

public sealed class RequestOutboundPipelineExecutor : IRequestOutboundPipelineExecutor
{
    private readonly RequestOutboundPipelineTypes _middlewareTypes;

    public RequestOutboundPipelineExecutor(RequestOutboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(RequestOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IRequestOutboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}
