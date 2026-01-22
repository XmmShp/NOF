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