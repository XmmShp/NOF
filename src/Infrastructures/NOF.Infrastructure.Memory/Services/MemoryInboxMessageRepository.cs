namespace NOF.Infrastructure.Memory;

public sealed class MemoryInboxMessageRepository : MemoryRepository<NOFInboxMessage, Guid>, IInboxMessageRepository
{
    public MemoryInboxMessageRepository(MemoryPersistenceContext context)
        : base(context, static message => message.Id)
    {
    }

    public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Context.Set<NOFInboxMessage>().Any(message => message.Id == messageId));
    }
}
