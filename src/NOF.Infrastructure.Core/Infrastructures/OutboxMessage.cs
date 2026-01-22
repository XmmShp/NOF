using NOF.Contract.Annotations;
using System.ComponentModel;
using System.Diagnostics;

namespace NOF;

/// <summary>
/// Outbox 消息
/// 用于事务上下文中的消息添加和后台服务的消息读取
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboxMessage
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 消息内容（包装后的消息）
    /// </summary>
    public required IMessage Message { get; init; }

    /// <summary>
    /// 头信息字典
    /// </summary>
    public Dictionary<string, string?> Headers { get; init; } = [];

    /// <summary>
    /// 目标端点名称
    /// </summary>
    public string? DestinationEndpointName { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 重试次数（添加时默认为0）
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// 分布式追踪 TraceId（用于恢复追踪上下文）
    /// </summary>
    public ActivityTraceId? TraceId { get; init; }

    /// <summary>
    /// 分布式追踪 SpanId（用于恢复追踪上下文）
    /// </summary>
    public ActivitySpanId? SpanId { get; init; }
}