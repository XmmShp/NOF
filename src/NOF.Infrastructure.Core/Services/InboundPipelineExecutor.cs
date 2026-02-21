using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Default implementation of <see cref="IInboundPipelineExecutor"/>.
/// Middleware instances are resolved from DI (scoped, like ASP.NET Core's <c>IMiddleware</c>).
/// Middleware ordering is determined at startup by the topological sort of
/// <see cref="IInboundMiddlewareStep"/> instances, stored in <see cref="InboundPipelineTypes"/>.
/// </summary>
public sealed class InboundPipelineExecutor : IInboundPipelineExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly InboundPipelineTypes _middlewareTypes;

    public InboundPipelineExecutor(
        IServiceProvider serviceProvider,
        InboundPipelineTypes middlewareTypes)
    {
        _serviceProvider = serviceProvider;
        _middlewareTypes = middlewareTypes;
    }

    public ValueTask ExecuteAsync(InboundContext context, InboundDelegate inbound, CancellationToken cancellationToken)
    {
        // Resolve all middleware from DI in the order determined by the dependency graph
        var pipeline = inbound;
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IInboundMiddleware)_serviceProvider.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, ct2 => next(ct2), ct);
        }

        return pipeline(cancellationToken);
    }
}
