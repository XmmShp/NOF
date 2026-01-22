namespace NOF;

/// <summary>
/// 收件箱消息实体
/// 用于记录需要可靠处理的消息
/// </summary>
public class InboxMessage
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTime CreatedAt { get; }

    public InboxMessage(Guid id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 收件箱消息仓储接口
/// 负责管理收件箱消息的持久化
/// </summary>
public interface IInboxMessageRepository
{
    /// <summary>
    /// 添加收件箱消息
    /// </summary>
    /// <param name="message">收件箱消息</param>
    void Add(InboxMessage message);

    /// <summary>
    /// 根据消息ID检查消息是否存在
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);
}
