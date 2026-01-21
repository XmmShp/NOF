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
    public Guid Id { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容（JSON序列化）
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 消息处理时间
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// 消息状态
    /// </summary>
    public InboxMessageStatus Status { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    public InboxMessage()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Status = InboxMessageStatus.Pending;
        RetryCount = 0;
    }

    /// <summary>
    /// 创建收件箱消息
    /// </summary>
    /// <param name="messageType">消息类型</param>
    /// <param name="content">消息内容</param>
    /// <returns>收件箱消息实例</returns>
    public static InboxMessage Create(string messageType, string content)
    {
        return new InboxMessage
        {
            MessageType = messageType,
            Content = content
        };
    }
}

/// <summary>
/// 收件箱消息状态枚举
/// </summary>
public enum InboxMessageStatus
{
    /// <summary>
    /// 等待处理
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 处理成功
    /// </summary>
    Processed = 1,

    /// <summary>
    /// 处理失败
    /// </summary>
    Failed = 2
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
