using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting;

public sealed class RequestOutboundPipelineExecutor
{
    private readonly RequestOutboundPipelineTypes _middlewareTypes;
    private readonly IServiceProvider _services;

    public RequestOutboundPipelineExecutor(RequestOutboundPipelineTypes middlewareTypes, IServiceProvider services)
    {
        _middlewareTypes = middlewareTypes;
        _services = services;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(RequestOutboundContext context, HandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IRequestOutboundMiddleware)_services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        return pipeline(cancellationToken);
    }
}
