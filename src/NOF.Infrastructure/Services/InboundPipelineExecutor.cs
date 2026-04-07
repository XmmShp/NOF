using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IInboundPipelineExecutor"/>.
/// Middleware instances are resolved from DI (scoped, like ASP.NET Core's <c>IMiddleware</c>).
/// Middleware ordering is determined at startup by the topological sort of
/// <see cref="IInboundMiddlewareStep{TMiddleware}"/> registrations, stored in <see cref="InboundPipelineTypes"/>.
/// </summary>
public sealed class InboundPipelineExecutor : IInboundPipelineExecutor
{
    private readonly InboundPipelineTypes _middlewareTypes;

    public InboundPipelineExecutor(InboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
    }

    public ValueTask ExecuteAsync(InboundContext context, InboundDelegate inbound, CancellationToken cancellationToken)
    {
        // Resolve all middleware from DI in the order determined by the dependency graph
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
