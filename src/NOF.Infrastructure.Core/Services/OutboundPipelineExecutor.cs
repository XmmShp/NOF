using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Default implementation of <see cref="IOutboundPipelineExecutor"/>.
/// </summary>
public sealed class OutboundPipelineExecutor : IOutboundPipelineExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboundPipelineTypes _middlewareTypes;

    public OutboundPipelineExecutor(
        IServiceProvider serviceProvider,
        OutboundPipelineTypes middlewareTypes)
    {
        _serviceProvider = serviceProvider;
        _middlewareTypes = middlewareTypes;
    }

    public ValueTask ExecuteAsync(OutboundContext context, OutboundDelegate dispatch, CancellationToken cancellationToken)
    {
        // Build pipeline: dispatch is the terminal step, then wrap with each middleware in reverse order
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IOutboundMiddleware)_serviceProvider.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, ct2 => next(ct2), ct);
        }

        return pipeline(cancellationToken);
    }
}
