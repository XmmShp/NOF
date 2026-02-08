using System.ComponentModel;

namespace NOF;

/// <summary>
/// Transactional message collector interface.
/// Collects outbox messages without depending on any other components.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IOutboxMessageCollector
{
    void AddMessage(OutboxMessage message);
    IReadOnlyList<OutboxMessage> GetMessages();
    void Clear();
}

/// <summary>
/// Transactional message collector implementation.
/// Serves as the lowest-level component with no business dependencies.
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
