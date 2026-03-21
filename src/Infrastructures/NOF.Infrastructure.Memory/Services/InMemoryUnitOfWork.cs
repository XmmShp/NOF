using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Memory;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly InMemoryPersistenceSession _session;
    private readonly IEventPublisher _eventPublisher;

    public InMemoryUnitOfWork(InMemoryPersistenceSession session, IEventPublisher eventPublisher)
    {
        _session = session;
        _eventPublisher = eventPublisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var trackedAggregates = _session.DrainTrackedAggregates();
        var domainEvents = trackedAggregates
            .SelectMany(aggregateRoot =>
            {
                var events = aggregateRoot.Events.ToArray();
                aggregateRoot.Events.Clear();
                return events;
            })
            .ToArray();

        foreach (var domainEvent in domainEvents)
        {
            await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
        }

        return _session.DrainChangeCount();
    }
}
