using NOF.Application;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryUnitOfWork : IUnitOfWork
{
    private readonly MemoryPersistenceStore _store;
    private readonly IInvocationContext _invocationContext;
    private readonly IEventPublisher _eventPublisher;

    public MemoryUnitOfWork(MemoryPersistenceStore store, IInvocationContext invocationContext, IEventPublisher eventPublisher)
    {
        _store = store;
        _invocationContext = invocationContext;
        _eventPublisher = eventPublisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var context = _store.CreateContext(_invocationContext.TenantId);
        var changedEntities = context.ConsumeTrackedEntities();

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
