using NOF.Domain;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryPersistenceContext : ICloneable
{
    private int _pendingChanges;
    private readonly ConcurrentDictionary<IAggregateRoot, byte> _trackedEntities = new(ReferenceEqualityComparer.Instance);

    public MemoryPersistenceContext() : this(new ConcurrentDictionary<Type, IMemoryPersistenceTable>())
    {
    }

    public MemoryPersistenceContext(ConcurrentDictionary<Type, IMemoryPersistenceTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        Tables = tables;
    }

    public ConcurrentDictionary<Type, IMemoryPersistenceTable> Tables { get; }

    public List<TAggregateRoot> Set<TAggregateRoot>()
        where TAggregateRoot : class, ICloneable
        => ((MemoryPersistenceTable<TAggregateRoot>)Tables.GetOrAdd(typeof(TAggregateRoot), _ => new MemoryPersistenceTable<TAggregateRoot>())).Items;

    internal bool TrackEntity(IAggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        if (!_trackedEntities.TryAdd(aggregateRoot, 0))
        {
            return false;
        }

        Interlocked.Increment(ref _pendingChanges);
        return true;
    }

    internal List<IAggregateRoot> ConsumeTrackedEntities()
    {
        var trackedEntities = _trackedEntities.Keys.ToList();
        _trackedEntities.Clear();
        return trackedEntities;
    }

    internal void RestoreFrom(MemoryPersistenceContext snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Tables.Clear();
        foreach (var table in snapshot.Tables)
        {
            Tables[table.Key] = (IMemoryPersistenceTable)table.Value.Clone();
        }

        Interlocked.Exchange(ref _pendingChanges, snapshot._pendingChanges);
        _trackedEntities.Clear();
    }

    public object Clone()
    {
        var tables = new ConcurrentDictionary<Type, IMemoryPersistenceTable>();
        foreach (var table in Tables)
        {
            tables[table.Key] = (IMemoryPersistenceTable)table.Value.Clone();
        }

        return new MemoryPersistenceContext(tables)
        {
            _pendingChanges = _pendingChanges
        };
    }
}
