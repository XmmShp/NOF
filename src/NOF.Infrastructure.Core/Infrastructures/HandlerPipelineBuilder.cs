using System.ComponentModel;

namespace NOF;

/// <summary>
/// Handler 管道构建器
/// 用于组合多个中间件形成执行管道
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IHandlerPipelineBuilder
{
    /// <summary>
    /// 添加中间件到管道
    /// </summary>
    IHandlerPipelineBuilder Use(IHandlerMiddleware middleware);
}

/// <summary>
/// Handler 管道构建器的默认实现
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
