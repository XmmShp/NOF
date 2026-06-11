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
        _middlewareTypes.Freeze(services);
    }

    public ValueTask ExecuteAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IRequestOutboundMiddleware)_services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = (currentContext, currentRequest, ct) => middleware.InvokeAsync(currentContext, currentRequest, next, ct);
        }

        return pipeline(context, request, cancellationToken);
    }
}
