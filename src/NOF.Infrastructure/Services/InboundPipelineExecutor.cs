using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IInboundPipelineExecutor"/>.
/// Middleware instances are resolved from DI (scoped, like ASP.NET Core's <c>IMiddleware</c>).
/// Middleware ordering is determined at startup by dependency sorting in <see cref="InboundPipelineTypes"/>.
/// </summary>
public sealed class InboundPipelineExecutor : IInboundPipelineExecutor
{
    private readonly InboundPipelineTypes _middlewareTypes;

    public InboundPipelineExecutor(InboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
        _middlewareTypes.Freeze();
    }

    public ValueTask ExecuteAsync(InboundContext context, InboundDelegate inbound, CancellationToken cancellationToken)
    {
        var pipeline = inbound;
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IInboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, ct2 => next(ct2), ct);
        }

        return pipeline(cancellationToken);
    }
}
