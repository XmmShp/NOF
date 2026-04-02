using NOF.Application;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryUnitOfWork : IUnitOfWork
{
    private readonly MemoryPersistenceContext _context;
    private readonly IEventPublisher _eventPublisher;

    public MemoryUnitOfWork(MemoryPersistenceContext context, IEventPublisher eventPublisher)
    {
        _context = context;
        _eventPublisher = eventPublisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var changedEntities = _context.ConsumeTrackedEntities();

        var domainEvents = changedEntities
            .SelectMany(aggregateRoot =>
            {
                var events = aggregateRoot.Events.ToArray();
                aggregateRoot.Events.Clear();
                return events;
            })
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
        }

        return changedEntities.Count;
    }
}
