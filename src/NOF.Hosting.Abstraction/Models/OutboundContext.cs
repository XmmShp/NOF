using NOF.Contract;
using System.ComponentModel;

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
    /// 独立于执行上下文的出站头集合。中间件可将需要跨进程/跨协议传播的键值写入此处。
    /// 这些值不会反向污染当前的 <see cref="IExecutionContext"/>。
    /// </summary>
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 响应结果，可能由 Rider 或短路的中间件设置。
    /// </summary>
    public IResult? Response { get; set; }
}
