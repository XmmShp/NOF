using NOF.Domain;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public abstract class MemoryRepository<TAggregateRoot, TKey> : IRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot
    where TKey : notnull
{
    private readonly Func<string> _partitionNameFactory;
    private readonly Func<TAggregateRoot, TKey> _keySelector;
    private readonly Func<TAggregateRoot, TAggregateRoot> _cloner;
    private readonly IEqualityComparer<TKey>? _keyComparer;

    protected MemoryRepository(
        MemoryPersistenceStore store,
        MemoryPersistenceSession session,
        string partitionName,
        Func<TAggregateRoot, TKey> keySelector,
        Func<TAggregateRoot, TAggregateRoot> cloner,
        IEqualityComparer<TKey>? keyComparer = null)
        : this(store, session, () => partitionName, keySelector, cloner, keyComparer)
    {
    }

    protected MemoryRepository(
        MemoryPersistenceStore store,
        MemoryPersistenceSession session,
        Func<string> partitionNameFactory,
        Func<TAggregateRoot, TKey> keySelector,
        Func<TAggregateRoot, TAggregateRoot> cloner,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        Store = store;
        Session = session;
        _partitionNameFactory = partitionNameFactory;
        _keySelector = keySelector;
        _cloner = cloner;
        _keyComparer = keyComparer;
    }

    protected MemoryPersistenceStore Store { get; }

    protected MemoryPersistenceSession Session { get; }

    protected ConcurrentDictionary<TKey, TAggregateRoot> Items
        => Store.GetPartition(_partitionNameFactory(), _keySelector, _cloner, _keyComparer).Items;

    protected virtual IEnumerable<TAggregateRoot> OrderItems(IEnumerable<TAggregateRoot> items)
        => items;

    public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (keyValues is not [TKey key])
        {
            return ValueTask.FromResult<TAggregateRoot?>(null);
        }

        var entity = Items.TryGetValue(key, out var found) ? Track(found) : null;
        return ValueTask.FromResult(entity);
    }

    public virtual async IAsyncEnumerable<TAggregateRoot> FindAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var entity in OrderItems(Items.Values))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Track(entity);
            await Task.CompletedTask;
        }
    }

    public virtual void Add(TAggregateRoot entity)
    {
        Items[_keySelector(entity)] = entity;
        Session.RegisterChange(entity);
    }

    public virtual void Remove(TAggregateRoot entity)
    {
        Items.TryRemove(_keySelector(entity), out _);
        Session.RegisterChange(entity);
    }

    protected TAggregateRoot Track(TAggregateRoot aggregateRoot)
    {
        Session.Track(aggregateRoot);
        return aggregateRoot;
    }
}
