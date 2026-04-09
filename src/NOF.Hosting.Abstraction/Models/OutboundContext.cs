using System.ComponentModel;
using NOF.Contract;

namespace NOF.Hosting;

/// <summary>
/// 出站消息管道上下文。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboundContext
{
    /// <summary>
    /// 出站消息（命令、通知或请求负载）。对于无参数方法可为 null。
    /// </summary>
    public object? Message { get; init; }

    /// <summary>
    /// 服务定位器，用于在管道执行期间解析依赖。
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// 响应结果，可能由 Rider 或短路的中间件设置。
    /// </summary>
    public IResult? Response { get; set; }
}

