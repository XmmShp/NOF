using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Executes the outbound middleware pipeline for messages being sent out.
/// Mirrors <see cref="InboundPipelineExecutor"/> for the outbound direction.
/// Middleware instances are resolved from DI (scoped) in the order determined
/// by the topological sort of <see cref="IOutboundMiddlewareStep"/> instances.
/// </summary>
public interface IOutboundPipelineExecutor
{
    /// <summary>
    /// Runs the outbound pipeline with the given terminal dispatch delegate.
    /// Middleware can wrap the dispatch (e.g., for tracing spans).
    /// The <paramref name="dispatch"/> delegate is called as the terminal step of the pipeline.
    /// </summary>
    ValueTask ExecuteAsync(OutboundContext context, OutboundDelegate dispatch, CancellationToken cancellationToken);
}

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
