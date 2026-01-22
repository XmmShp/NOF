using NOF.Contract.Annotations;
using System.ComponentModel;

namespace NOF;

/// <summary>
/// 事务性消息收集器接口
/// 负责收集事务性消息，不依赖任何其他组件
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IOutboxMessageCollector
{
    void AddMessage(OutboxMessage message);
    IReadOnlyList<OutboxMessage> GetMessages();
    void Clear();
}

/// <summary>
/// Outbox 消息
/// 用于事务上下文中的消息添加和后台服务的消息读取
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboxMessage
{
    /// <summary>
    /// 消息ID（从MessageWrapper获取）
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 消息内容（包装后的消息）
    /// </summary>
    public required IMessage Message { get; init; }

    /// <summary>
    /// 目标端点名称
    /// </summary>
    public string? DestinationEndpointName { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 重试次数（添加时默认为0）
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// 分布式追踪 TraceId（用于恢复追踪上下文）
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 分布式追踪 SpanId（用于恢复追踪上下文）
    /// </summary>
    public string? SpanId { get; init; }

    private OutboxMessage() { }

    /// <summary>
    /// 创建OutboxMessage
    /// </summary>
    /// <param name="id">消息 Id</param>
    /// <param name="message">消息</param>
    /// <param name="destinationEndpointName">目标端点名称</param>
    /// <param name="traceId">追踪ID</param>
    /// <param name="spanId">Span ID</param>
    /// <param name="createdAt">创建时间</param>
    /// <param name="retryCount">重试次数</param>
    /// <returns>Outbox消息</returns>
    public static OutboxMessage Create(
        Guid id,
        IMessage message,
        string? destinationEndpointName = null,
        string? traceId = null,
        string? spanId = null,
        DateTimeOffset? createdAt = null,
        int retryCount = 0)
    {
        return new OutboxMessage
        {
            Id = id,
            Message = message,
            DestinationEndpointName = destinationEndpointName,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            TraceId = traceId,
            SpanId = spanId,
            RetryCount = retryCount
        };
    }
}