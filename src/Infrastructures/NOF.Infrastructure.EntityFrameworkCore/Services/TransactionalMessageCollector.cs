namespace NOF;

/// <summary>
/// 事务性消息收集器实现
/// 作为最底层组件，不依赖任何其他业务组件
/// </summary>
internal sealed class TransactionalMessageCollector : ITransactionalMessageCollector
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
