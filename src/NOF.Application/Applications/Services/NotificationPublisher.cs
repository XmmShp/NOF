namespace NOF;

public interface INotificationPublisher
{
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// 延迟通知发布器接口
/// 用于在不使用 HandlerBase 的情况下手动添加通知到事务性上下文
/// </summary>
public interface IDeferredNotificationPublisher
{
    /// <summary>
    /// 添加通知到事务性上下文
    /// 通知将在 UnitOfWork.SaveChangesAsync 时统一持久化到 Outbox
    /// </summary>
    void Publish(INotification notification);
}
