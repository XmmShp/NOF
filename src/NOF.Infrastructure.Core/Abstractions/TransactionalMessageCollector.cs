namespace NOF;

/// <summary>
/// 事务性消息收集器接口
/// 负责收集事务性消息，不依赖任何其他组件
/// </summary>
public interface ITransactionalMessageCollector
{
    void AddMessage(OutboxMessage message);
    IReadOnlyList<OutboxMessage> GetMessages();
    void Clear();
}
