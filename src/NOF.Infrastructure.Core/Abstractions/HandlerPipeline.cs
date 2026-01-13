namespace NOF;

/// <summary>
/// Handler 执行上下文
/// 包含 Handler 执行过程中的元数据
/// </summary>
public sealed class HandlerContext
{
    /// <summary>
    /// Handler 类型名称
    /// </summary>
    public required string HandlerType { get; init; }

    /// <summary>
    /// 消息类型名称
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// 消息实例
    /// </summary>
    public required object Message { get; init; }

    /// <summary>
    /// Handler 实例
    /// </summary>
    public required object Handler { get; init; }

    /// <summary>
    /// 自定义属性字典，用于在中间件之间传递数据
    /// </summary>
    public Dictionary<string, object> Items { get; } = new();
}

/// <summary>
/// Handler 执行管道的委托
/// </summary>
public delegate ValueTask HandlerDelegate(CancellationToken cancellationToken);

/// <summary>
/// Handler 中间件接口
/// 用于在 Handler 执行前后插入横切关注点（如事务、日志、验证等）
/// </summary>
public interface IHandlerMiddleware
{
    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    /// <param name="context">Handler 执行上下文</param>
    /// <param name="next">管道中的下一个中间件或最终的 Handler</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

/// <summary>
/// Handler 管道构建器
/// 用于组合多个中间件形成执行管道
/// </summary>
public interface IHandlerPipelineBuilder
{
    /// <summary>
    /// 添加中间件到管道
    /// </summary>
    IHandlerPipelineBuilder Use(IHandlerMiddleware middleware);
}
