namespace NOF.Infrastructure.Memory;

public sealed class MemoryInboxMessageRepository : MemoryRepository<NOFInboxMessage, Guid>, IInboxMessageRepository
{
    public MemoryInboxMessageRepository(MemoryPersistenceStore store, MemoryPersistenceSession session)
        : base(store, session, "nof:inbox", static message => message.Id, static message => new NOFInboxMessage(message.Id)
        {
            CreatedAt = message.CreatedAt
        })
    {
    }

    public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Items.ContainsKey(messageId));
    }

    protected override IEnumerable<NOFInboxMessage> OrderItems(IEnumerable<NOFInboxMessage> items)
        => items.OrderBy(message => message.CreatedAt);
}
