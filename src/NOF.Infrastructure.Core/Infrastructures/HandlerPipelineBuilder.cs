using System.ComponentModel;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Handler pipeline builder for composing multiple middleware into an execution pipeline.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IHandlerPipelineBuilder
{
    /// <summary>
    /// Adds a middleware to the pipeline.
    /// </summary>
    IHandlerPipelineBuilder Use(IHandlerMiddleware middleware);
}

/// <summary>
/// Default implementation of the handler pipeline builder.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HandlerPipelineBuilder : IHandlerPipelineBuilder
{
    private readonly List<IHandlerMiddleware> _middlewares = [];

    public IHandlerPipelineBuilder Use(IHandlerMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    internal HandlerDelegate Build(HandlerContext context, HandlerDelegate handler)
    {
        // Build the pipeline from back to front
        var pipeline = handler;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = cancellationToken => middleware.InvokeAsync(context, ct => next(ct), cancellationToken);
        }

        return pipeline;
    }
}
