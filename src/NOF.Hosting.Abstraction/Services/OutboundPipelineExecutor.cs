namespace NOF.Hosting;

public sealed class RequestOutboundPipelineExecutor
{
    private readonly IReadOnlyList<IRequestOutboundMiddleware> _middlewares;

    public RequestOutboundPipelineExecutor(IEnumerable<IRequestOutboundMiddleware> middlewares)
    {
        _middlewares = new DependencyGraph<IRequestOutboundMiddleware>(middlewares).GetExecutionOrder();
    }

    public ValueTask ExecuteAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate dispatch, CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentRequest, ct) => middleware.InvokeAsync(currentContext, currentRequest, next, ct);
        }

        return pipeline(context, request, cancellationToken);
    }
}
