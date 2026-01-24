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
/// 事务性消息收集器实现
/// 作为最底层组件，不依赖任何其他业务组件
/// </summary>
public sealed class OutboxMessageCollector : IOutboxMessageCollector
{
    private readonly List<OutboxMessage> _messages = [];

    public void AddMessage(OutboxMessage message)
    {
        _messages.Add(message);
    }

    public IReadOnlyList<OutboxMessage> GetMessages() => _messages.AsReadOnly();

    public void Clear()
    {
        _messages.Clear();
    }
}