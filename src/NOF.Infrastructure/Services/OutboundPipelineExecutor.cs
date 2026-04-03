using Microsoft.Extensions.DependencyInjection;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IOutboundPipelineExecutor"/>.
/// </summary>
public sealed class OutboundPipelineExecutor : IOutboundPipelineExecutor
{
    private readonly OutboundPipelineTypes _middlewareTypes;

    public OutboundPipelineExecutor(OutboundPipelineTypes middlewareTypes)
    {
        _middlewareTypes = middlewareTypes;
    }

    public ValueTask ExecuteAsync(OutboundContext context, OutboundDelegate dispatch, CancellationToken cancellationToken)
    {
        // Build pipeline: dispatch is the terminal step, then wrap with each middleware in reverse order
        var pipeline = dispatch;

        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IOutboundMiddleware)context.Services.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, ct2 => next(ct2), ct);
        }

        return pipeline(cancellationToken);
    }
}
