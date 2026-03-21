using NOF.Domain;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public sealed class InMemoryPersistenceSession
{
    private readonly ConcurrentDictionary<IAggregateRoot, byte> _trackedAggregates = new();
    private int _changeCount;

    public void Track(IAggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);
        _trackedAggregates.TryAdd(aggregateRoot, 0);
    }

    public void RegisterChange(IAggregateRoot aggregateRoot)
    {
        Track(aggregateRoot);
        Interlocked.Increment(ref _changeCount);
    }

    public IReadOnlyCollection<IAggregateRoot> DrainTrackedAggregates()
    {
        var aggregates = _trackedAggregates.Keys.ToArray();
        _trackedAggregates.Clear();
        return aggregates;
    }

    public int DrainChangeCount()
        => Interlocked.Exchange(ref _changeCount, 0);
}
