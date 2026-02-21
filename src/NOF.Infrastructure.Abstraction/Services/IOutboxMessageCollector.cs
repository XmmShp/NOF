using System.ComponentModel;

namespace NOF.Infrastructure.Abstraction;

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
