namespace NOF;

/// <summary>
/// Handler 管道构建器的默认实现
/// </summary>
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
        // 从后向前构建管道
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
