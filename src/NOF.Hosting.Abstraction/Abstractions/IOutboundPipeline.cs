using System.ComponentModel;

namespace NOF.Hosting;

/// <summary>
/// Outbound pipeline delegate。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask OutboundDelegate(CancellationToken cancellationToken);

/// <summary>
/// 出站中间件接口，与 <see cref="NOF.Application.IInboundMiddleware"/> 对称，用于处理出站消息的横切逻辑。
/// </summary>
public interface IOutboundMiddleware
{
    /// <summary>
    /// 执行出站中间件逻辑。
    /// </summary>
    ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken);
}

/// <summary>
/// 执行出站中间件管道。
/// </summary>
public interface IOutboundPipelineExecutor
{
    /// <summary>
    /// 使用给定的终端分发委托执行出站管道。
    /// </summary>
    ValueTask ExecuteAsync(OutboundContext context, OutboundDelegate dispatch, CancellationToken cancellationToken);
}

