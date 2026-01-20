namespace NOF;

/// <summary>
/// 事务性消息仓储接口
/// 在事务上下文中操作 Outbox 消息
/// 基础设施层必须实现此接口以提供：
/// 1. 持久化能力（Outbox 表）
/// 2. 可靠发送能力（后台服务）
/// </summary>
public interface ITransactionalMessageRepository
{
    /// <summary>
    /// 在当前事务上下文中添加消息到 Outbox
    /// </summary>
    void Add(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 抢占式获取待发送的消息，避免多实例重复处理
    /// 返回的消息会被标记为处理中状态，其他实例无法重复获取
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingMessagesAsync(int batchSize = 100, TimeSpan? claimTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记消息已发送
    /// </summary>
    Task MarkAsSentAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记消息发送失败
    /// </summary>
    Task RecordDeliveryFailureAsync(Guid messageId, string errorMessage, CancellationToken cancellationToken = default);
}
