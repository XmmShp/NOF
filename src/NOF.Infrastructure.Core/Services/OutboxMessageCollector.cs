using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

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
